# ADR-0012 : Purge RGPD automatisée — seuil, critère, action, et mécanisme asymétrique par cloud

**Statut** : Acceptée, implémentée et déployée (`terraform apply` exécuté le 12/09/2026)
**Date** : 2026-09-12
**Sprint** : 6

## Contexte

`docs/rgpd-classification-donnees.md` §2/§5 identifiait un chantier non commencé : « Purge automatisée par catégorie métier (au-delà de l'expiration technique des backups/logs) — il n'y a pas de job qui purge une donnée personnelle avant sa rétention technique par catégorie métier (ex. "supprimer les clients inactifs depuis 3 ans") ». Contrairement au fix du même sprint sur `orders.customer_id` orphelin (ADR implicite, voir ce document §3), ce chantier suppose une décision de seuil que le code ne peut pas trancher seul — ce n'est pas un bug à corriger, c'est une politique de rétention à choisir.

Décidé avec l'utilisateur (12/09/2026) :
- **Seuil** : 3 ans d'inactivité.
- **Critère d'inactivité** : date de la dernière commande (`Order.CreatedAt` le plus récent) ; à défaut de commande, `Customer.CreatedAt`.
- **Action** : anonymisation (`Customer.Anonymize()`, déjà écrit pour le fix `orders.customer_id`), jamais suppression physique — que le client ait des commandes ou non, pour rester cohérent avec le chemin d'effacement à la demande plutôt que d'avoir deux comportements d'anonymisation légèrement différents.

Restait à trancher : **comment** ce job tourne concrètement. Le réflexe initial (« job infra externe, Azure Automation + AWS Lambda », symétrique aux rotations de secrets déjà en place) s'est heurté à une contrainte déjà documentée dans ce projet : **ADR-0010** établit qu'un Azure Automation Runbook n'a aucun accès réseau au VNet privé où vit Postgres (confirmé par la doc Microsoft officielle citée dans cette ADR), ce qui a précisément produit la procédure manuelle Kudu pour la rotation d'`arkcloud_app` plutôt qu'un Runbook automatisé. Un job de purge qui exécute du vrai SQL (`UPDATE customers SET ...`) tombe dans exactement la même contrainte.

## Options envisagées

1. **Job infra symétrique des deux côtés** (Azure Automation Runbook + AWS Lambda) — rejetée : impossible nativement côté Azure pour la raison ci-dessus, sauf à répéter le contournement Hybrid Runbook Worker qu'ADR-0010 a déjà écarté pour un coût récurrent disproportionné (~15-70 $/mois) face à un besoin qui ne se déclenche qu'occasionnellement.
2. **Un seul mécanisme applicatif des deux côtés** (BackgroundService .NET dans `ArkCloud.API`, y compris sur AWS) — cohérente et simple à raisonner (un seul code testé), mais renonce à réutiliser la Lambda `secret-rotation` déjà en place et déjà validée en conditions réelles côté AWS, qui a déjà l'accès réseau nécessaire au VPC.
3. **Accepter le coût d'un Hybrid Runbook Worker côté Azure** pour garder un mécanisme infra symétrique — rejetée pour la même raison de fond qu'ADR-0010 : un coût récurrent pour un besoin peu fréquent.
4. **Mécanisme asymétrique, adapté à ce que chaque cloud permet réellement sans nouvelle infrastructure** : côté Azure, un `BackgroundService` .NET dans `ArkCloud.API` (l'App Service a déjà l'intégration VNet vers Postgres — c'est exactement ce qui permet à Kudu de fonctionner) ; côté AWS, une nouvelle Lambda dédiée (`modules/aws/gdpr-purge`) qui réutilise le même VPC/sous-réseaux/security group que la Lambda `secret-rotation` existante.

## Décision

Option 4. Asymétrie assumée consciemment, comme celle déjà actée par ADR-0010 entre la rotation `arkcloud_app` automatique côté AWS et manuelle côté Azure — pas une nouveauté dans la façon dont ce projet gère ce type de contrainte cross-cloud.

**Azure** : `CustomerRetentionPurgeHostedService` (backend `ArkCloud.API/HostedServices/`), un `BackgroundService` qui tourne toutes les 24h, appelle `CustomerRetentionPurgeService.PurgeInactiveCustomersAsync()` (`ArkCloud.Application`, framework-agnostique) dans un scope DI dédié. Activé uniquement par la config `Gdpr:RunRetentionPurgeInProcess` (défaut `false`), posée à `true` explicitement par Terraform sur `app_service_api` (`environments/dev/main.tf`, `extra_app_settings`) et jamais sur `app_service_web` — un conteneur qui oublierait de la définir ne fait rien, plutôt que de démarrer un job de fond par accident.

**AWS** : nouvelle Lambda `modules/aws/gdpr-purge`, planifiée par EventBridge (`rate(1 day)` par défaut, même cadence que le hosted service Azure), connectée comme `arkcloud_app` (lecture seule du secret, aucun accès admin nécessaire) dans le même VPC/sous-réseaux/security group que la Lambda `secret-rotation`. Volontairement une Lambda séparée plutôt qu'un mode ajouté à `secret-rotation`'s existante : celle-ci est pilotée par la state machine à quatre étapes de Secrets Manager (`createSecret`/`setSecret`/`testSecret`/`finishSecret`), et aucun secret n'est tourné ici — un troisième mode forcé dans ce moule serait un moins bon choix qu'une seconde fonction dédiée, qui réutilise le réseau déjà prouvé plutôt que le mécanisme d'invocation.

Les deux mécanismes appellent la même logique de fond au niveau SQL (les mêmes colonnes réécrites, le même format d'email de repli `anonymized+{id sans tirets}@arkcloud.invalid`, la même requête d'éligibilité conceptuelle — dernière commande, sinon date de création du client), pour qu'un client anonymisé par l'un ou l'autre chemin soit indiscernable.

## Conséquences

**Positives**
- Zéro nouvelle infrastructure permanente et payante des deux côtés — réutilise l'App Service déjà VNet-intégré côté Azure, réutilise le VPC/sous-réseaux/security group déjà provisionnés côté AWS.
- Cohérent avec un précédent déjà documenté et assumé (ADR-0010) plutôt qu'une incohérence découverte plus tard.
- Idempotent des deux côtés : un client déjà anonymisé (détecté via le suffixe d'email `@arkcloud.invalid`) n'est jamais retraité, donc une cadence quotidienne (largement plus fréquente que nécessaire vu un seuil en années) ne coûte qu'une requête bon marché la plupart du temps.

**Négatives / compromis assumés**
- Deux implémentations distinctes de la même logique métier (C#/.NET côté Azure, Python/SQL côté AWS) à maintenir en parallèle plutôt qu'un seul code partagé — même compromis que la rotation `arkcloud_app` (PowerShell/Azure vs Python/AWS), pas une régression par rapport à l'existant.
- Le hosted service Azure tourne dans le même processus que l'API — une purge lente ou en erreur partage le pool de threads avec le trafic applicatif normal (mitigé : `try/catch` autour de chaque exécution pour ne jamais faire planter le host, et le volume attendu — des clients inactifs depuis 3 ans — devrait rester faible en pratique).
- Le zip de la Lambda a été construit via un script PowerShell (`build.ps1`) plutôt que le `build.sh` bash utilisé par la CI, faute de WSL/Git Bash fonctionnels sur la machine de l'utilisateur au moment du premier `apply` — fonctionnellement identique, pas garanti octet-pour-octet identique, donc le prochain `apply` déclenché par la CI verra potentiellement la Lambda comme modifiée une fois, sans conséquence.
- `terraform apply` exécuté le 12/09/2026 (9 ressources créées, 1 modifiée, 0 détruite), mais aucune exécution réelle de la purge (déclenchement quotidien Azure ou EventBridge AWS) n'a encore été observée — voir `docs/infra-roadmap.md` pour le suivi.

**Ce que ça bloque ou impose pour la suite**
- Observer une première exécution réelle de la purge des deux côtés, pour confirmer en conditions réelles ce que les tests (unitaires + intégration Testcontainers) ont déjà validé en isolation.
- Si le seuil de 3 ans ou le critère (dernière commande) devaient changer, c'est une nouvelle décision à documenter de la même façon — pas un simple changement de constante à faire sans repasser par cette conversation, exactement pour la raison qui a motivé cette ADR au départ (une politique de rétention n'est pas un détail d'implémentation).
