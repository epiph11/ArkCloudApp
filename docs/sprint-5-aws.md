# Sprint 5 — Fondation AWS & bascule multi-cloud

> **Spécification technique, racontée par ses décisions.**
> Suite directe du Sprint 4. Là où le précédent construisait une plateforme, celui-ci en construit une seconde — et c'est le fait qu'il y en ait deux qui produit les questions les plus intéressantes.

**Périmètre** : Steps 10 à 15 du roadmap, versant AWS.
**Statut** : clôturé.
**Livrable** : une plateforme AWS complète et fonctionnelle, en parallèle d'Azure, avec le même pipeline CI/CD alimentant les deux clouds.

---

## 1. La question qui précède toute la suite : un state, ou deux ?

Première décision du sprint, et la plus structurante : AWS partage-t-il le state Terraform d'Azure, ou vit-il dans un module racine séparé ?

**Décision retenue : un seul module racine, un seul state, un seul `terraform apply` pour les deux clouds.**

Le raisonnement : deux states séparés signifient deux `apply` à coordonner à la main, deux occasions de dériver, et la perte de toute possibilité de référencer une ressource d'un cloud depuis l'autre. Or les chantiers multi-cloud à venir (clé JWT unifiée, réplication de données, DNS de bascule) ont précisément besoin de ces références croisées.

**Le prix, assumé et visible tout au long du projet** : chaque `terraform plan` interroge les deux fournisseurs, ce qui allonge sensiblement les cycles ; et une dérive côté Azure apparaît dans un plan censé ne concerner qu'AWS. C'est un coût de friction réel, accepté en échange de la cohérence.

---

## 2. Réseau AWS — trois étages, deux zones, et une économie délibérée

`vpc-arkcloud-dev` découpé en trois étages, chacun sur deux zones de disponibilité :

- **public** — l'ALB uniquement, route vers l'Internet Gateway
- **ecs** — les tâches Fargate, sortie par NAT Gateway
- **database** — RDS, aucune route sortante vers Internet

La symétrie avec Azure est intentionnelle mais pas mécanique : Azure sépare `snet-api` et `snet-web` (deux App Services distincts), AWS regroupe les deux services dans l'étage `ecs` et les sépare par **security group** plutôt que par sous-réseau. Le niveau d'isolation est équivalent, l'outil diffère.

**Un VPC endpoint S3** ajouté délibérément : sans lui, tout le trafic S3 des tâches ECS (récupération d'images, logs) passerait par le NAT Gateway, facturé au Go transféré. L'endpoint est gratuit et court-circuite ce chemin. Décision de coût prise à la construction, pas après la facture.

**Point de coût identifié et documenté** : le NAT Gateway n'est **jamais** couvert par le Free Tier AWS, quel que soit l'âge du compte. C'est le poste de dépense structurel de cette architecture, et il a été explicitement constaté lors de l'investigation budgétaire du Sprint 6.

---

## 3. Security groups — l'isolation par ce qui n'est *pas* écrit

Quatre groupes, dont la conception repose sur une omission volontaire.

| Groupe | Entrant | Sortant |
|---|---|---|
| `sg-alb` | 443 et 80 depuis Internet | vers les tâches ECS uniquement |
| `sg-ecs-api` | depuis `sg-alb` seul | ouvert (images, secrets, CloudWatch via NAT) |
| `sg-ecs-web` | depuis `sg-alb` seul | ouvert (même raison) |
| `sg-database` | **depuis `sg-ecs-api` seul** | aucune règle |

La ligne qui compte est la dernière : **`sg-ecs-web` n'est jamais listé dans les sources autorisées de `sg-database`**. Blazor ne touche pas la base directement — il passe par l'API. L'isolation ne vient pas d'une restriction sortante sur le frontend, mais du fait que la base ne l'accepte tout simplement pas comme source.

C'est le même principe que côté Azure, où seul l'App Service API reçoit un role assignment sur Key Vault. Dans les deux cas, la sécurité est portée par une absence délibérée, documentée comme telle pour qu'un ajout futur soit un acte conscient.

Ce choix a eu une conséquence directe au Sprint 6 : quand la Lambda de rotation a eu besoin d'atteindre RDS, il a fallu l'ajouter explicitement — et c'est cette modification qui a révélé le piège des règles inline (voir Sprint 6).

---

## 4. Le registre d'images : ECR provisoire contre JFrog planifié

Le roadmap d'origine prévoyait JFrog Artifactory comme registre unifié multi-cloud dès ce sprint.

**Décision inverse prise en session** : partir sur **Amazon ECR**, provisoire, et différer JFrog sans date fixée.

Le raisonnement : ECR est déjà natif à ECS, zéro friction d'authentification, aucun outil externe à opérationnaliser. JFrog résout un vrai problème — un registre unique pour les deux clouds — mais ce problème n'est pas encore douloureux, alors que l'effort d'opérationnalisation, lui, est immédiat.

**Conséquence assumée, et c'est une vraie dette** : le registre n'est **pas** unifié. GHCR continue de servir Azure, ECR sert AWS. La même image est construite et poussée deux fois, à deux endroits. C'est contraire à l'intention initiale du roadmap, documenté explicitement comme tel plutôt que passé sous silence.

---

## 5. ALB et routage — là où les deux clouds divergent vraiment

Azure donne gratuitement deux hostnames distincts (`app-arkcloud-api-dev` et `app-arkcloud-web-dev`), parce que ce sont deux App Services séparés.

Un ALB n'a pas d'équivalent sans soit un second ALB, soit un vrai domaine personnalisé avec routage par hôte. Ni l'un ni l'autre n'était justifié à ce stade.

**Choix pragmatique retenu** : routage **par chemin** sur un ALB unique — `/api/*` vers le target group API, tout le reste (action par défaut) vers le target group web.

**Conséquence directe sur l'application** : le paramètre `Api__BaseUrl` de Blazor doit pointer sur `<alb-dns-name>/api` côté AWS, alors qu'il pointe sur un hôte séparé côté Azure. Les deux clouds ne sont donc **pas** interchangeables du point de vue de la configuration applicative — une asymétrie réelle, qui devra être résolue le jour où une bascule primaire/DR sera implémentée.

**HTTPS délibérément absent à ce stade** : un certificat ACM a besoin d'un domaine réel à valider, qui n'existe pas pour ce projet. Le port 443 est cependant déjà ouvert sur `sg-alb` en prévision. Traité au Sprint 6 par un certificat auto-signé, faute de domaine.

---

## 6. Secrets Manager — la même règle qu'Azure, avec une exception honnête

Comme pour Key Vault : **seuls les conteneurs de secrets sont créés par Terraform**, jamais les valeurs. Pas de `aws_secretsmanager_secret_version` dans le code.

**Une exception inévitable, présente sur les deux clouds** : le moteur de base de données exige son mot de passe maître comme argument Terraform direct (`aws_db_instance.password`, `azurerm_postgresql_flexible_server.administrator_password`). Cette valeur atterrit dans le state, quoi qu'on fasse. Les deux secrets applicatifs (JWT, chaîne de connexion) sont justement là pour tout ce qui **n'est pas** contraint par un schéma de ressource.

`administrator_password` est placé en `ignore_changes` — pour qu'un apply sans rapport ne touche jamais ce mot de passe par accident. Décision qui s'est révélée essentielle au Sprint 6 : c'est elle qui a rendu la rotation automatique possible sans conflit avec Terraform.

---

## 7. Le format du secret Postgres — une décision aux conséquences lointaines

Le secret `arkcloud/arkcloud-dev/postgres` contient une **chaîne de connexion .NET complète**, mappée telle quelle sur la variable d'environnement `ConnectionStrings__DefaultConnection` de la task definition ECS.

Ce choix découle du code applicatif : `ArkCloud.API` lit la chaîne entière via `GetConnectionString("DefaultConnection")`. C'est aussi la forme utilisée côté Azure, où Key Vault sert la même chaîne complète.

**Conséquence non anticipée, découverte au Sprint 6** : la Lambda de rotation fournie par AWS exige un secret au format JSON structuré. Ce format-là l'a rendue inutilisable, obligeant à écrire une Lambda de rotation personnalisée.

Décision instructive : elle était **correcte** au moment où elle a été prise (cohérence avec l'application et avec Azure), et elle a quand même produit un coût réel deux sprints plus tard. C'est le genre d'arbitrage qui mérite un ADR — un des manques identifiés et comblés depuis dans le roadmap.

---

## 8. Bugs réels rencontrés

### Port de conteneur incohérent — 8081 contre 8080

Le conteneur web exposait 8081, le target group ALB pointait sur 8080. Les health checks échouaient en boucle, les tâches étaient tuées et relancées indéfiniment.

Symptôme trompeur : cela ressemblait à une application qui plante au démarrage, alors que l'application allait parfaitement bien — personne ne frappait au bon endroit.

### `GET /health` renvoyait 404

Gap hérité du Sprint 4, où il avait été identifié et volontairement laissé ouvert (non bloquant sur App Service, dont le probe tolère les 404). Sur ECS, c'est bloquant : un health check en échec fait tuer la tâche.

Corrigé dans le repo applicatif, pour l'API **et** pour Blazor. Illustration nette qu'un gap « non bloquant » sur une plateforme peut devenir bloquant sur une autre.

### Filtre de métrique CloudWatch invalide

Les métriques d'erreur applicatives utilisaient un filtre JSON ciblant le champ `@l` de Serilog (`$.['@l'] = "Error"`). Rejeté par AWS à l'apply : `Invalid character(s) in term`.

**Diagnostic établi contre la documentation officielle, pas supposé** : la notation entre crochets n'est documentée que pour les noms de propriété contenant un **point**, pas comme échappement générique. Le caractère `@` n'a jamais été un sélecteur de propriété valide, avec ou sans crochets.

Corrigé par un filtre non structuré (`"?Error ?Fatal"`), qui traite la ligne de log comme du texte brut et n'a pas cette restriction. **Compromis assumé** : moins précis — le mot est cherché n'importe où dans la ligne, pas seulement dans le champ de niveau.

---

## 9. CloudTrail — l'équivalent AWS des logs d'audit

Piste d'audit complète : bucket S3 dédié avec versioning, chiffrement, blocage d'accès public et politique de cycle de vie ; intégration CloudWatch Logs ; notifications SNS.

Findings Checkov triés selon la même discipline qu'au Sprint 4 : quatre corrigés pour de vrai (versioning, expiration du cycle de vie, notifications de livraison, intégration CloudWatch Logs), six écartés avec justification écrite — dont le **journal d'accès S3 sur le bucket d'audit lui-même**, écarté parce que journaliser récursivement l'accès à un journal d'audit relève du durcissement de conformité sans exigence qui le motive à ce stade.

---

## 10. Bilan et état réel à la clôture

Les deux clouds hébergent une copie complète et fonctionnelle de l'application. Le pipeline CI/CD alimente les deux.

**Mais ils ne sont pas un système multi-cloud** — ce sont deux déploiements parallèles indépendants, et c'est important de le nommer :

- Bases de données séparées, aucune réplication
- Clés de signature JWT séparées — un token émis par un cloud est invalide sur l'autre
- Aucun routage ni bascule entre les deux
- Registres d'images différents (GHCR / ECR)
- Configuration applicative asymétrique (`Api__BaseUrl`)

Ce constat a directement produit la décision d'architecture cible formalisée en fin de sprint : **parallèle actif à court terme, primaire + DR en warm standby à terme** — avec trois chantiers explicitement identifiés comme bloquants (réplication PostgreSQL cross-cloud, couche DNS/health-check agnostique, unification de la clé JWT).

La valeur du sprint n'est donc pas seulement l'infrastructure livrée : c'est d'avoir rendu **visible et nommé** l'écart entre « deux clouds » et « multi-cloud ».
