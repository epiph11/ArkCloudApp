# Classification des données & RGPD (Sprint 6)

> Complète Step 16 septies du roadmap. Le système stocke des données de personnes physiques (clients, comptes utilisateurs) — ce document classe ces données par sensibilité et confronte chaque exigence RGPD (rétention, effacement, minimisation, résidence, chiffrement) à l'état réel du code et de l'infrastructure, pas à une intention.

**Méthode** : pour chaque exigence, l'état constaté vient d'une lecture directe du code (`ArkCloud.Domain`, `ArkCloud.Application`) et du Terraform (`ArkCloudInfra`), pas d'une supposition — même discipline que `docs/threat-model-stride.md`.

---

## 1. Classification des données

Deux entités portent des données à caractère personnel ; une troisième (`Order`) y est liée sans en contenir directement.

| Entité | Champs | Catégorie | Personne concernée |
|---|---|---|---|
| `Customer` | `FirstName`, `LastName`, `Email`, `Address` (rue/ville/pays) | Personnelle | Client final |
| `User` | `Email`, `PasswordHash`, `FirstName`, `LastName` | Personnelle (+ `PasswordHash` : sensible par nature, jamais en clair) | Compte applicatif (utilisateur du système, pas le client final — auth/JWT) |
| `Order` | `CustomerId` (référence), `Items` (produits/quantités/prix), `Status`, `CreatedAt` | Interne, liée indirectement à une personne via `CustomerId` | Client final (par référence) |

Aucune donnée de santé, biométrique, ou autre catégorie « sensible » au sens RGPD (art. 9) n'est stockée — uniquement de l'identité et du contact.

**Constat structurel, corrigé cette semaine** : `Order.CustomerId` était une simple colonne Guid, sans contrainte de clé étrangère configurée dans EF Core (`OrderConfiguration.cs` ne déclarait aucun `HasOne(Customer)`). Une contrainte FK (`ON DELETE RESTRICT`) a été ajoutée, et `CustomerAppService.DeleteAsync` anonymise désormais le client plutôt que de le supprimer si des commandes existent — voir §3.

## 2. Rétention

| Catégorie | Où | Rétention actuelle | Constat |
|---|---|---|---|
| Backups PostgreSQL (Azure) | `modules/azure/postgresql`, `backup_retention_days` | 7 jours | — |
| Backups PostgreSQL (AWS RDS) | `modules/aws/rds`, `backup_retention_period` | 1 jour | Plafonné par le Free Tier AWS (`FreeTierRestrictionError` au-delà, valeur exacte non documentée par AWS) — pas un choix RGPD, une contrainte de compte. Asymétrie Azure/AWS à assumer explicitement plutôt qu'ignorer. |
| Logs applicatifs (AWS, ECS→CloudWatch) | `modules/aws/ecs-service`, `log_retention_days` | 30 jours | — |
| Logs applicatifs (Azure, App Service→Log Analytics) | `modules/azure/monitoring`, `log_retention_days` | 30 jours | Symétrique avec AWS. |
| Logs rotation secrets (Lambda) | `modules/aws/secret-rotation` | 30 jours | — |
| CloudTrail (AWS, audit infra) | `modules/aws/cloudtrail` | 90 jours | Journal d'accès infra, pas de données client. |
| Flow logs réseau (Azure NSG) | `modules/azure/flow-logs` | 90 jours | Adresses IP source/destination — donnée à caractère personnel au sens large (RGPD), mais fenêtre déjà alignée sur la recommandation Checkov `CKV_AZURE_12`, pas un chantier ce sprint. |

**Purge automatisée — décidée et implémentée cette semaine** (ADR-0012) : seuil de 3 ans d'inactivité (date de la dernière commande, ou date de création du compte à défaut de commande), action = anonymisation (jamais suppression physique). Mécanisme volontairement asymétrique par cloud (Azure : `BackgroundService` in-process dans `ArkCloud.API` ; AWS : Lambda planifiée `modules/aws/gdpr-purge`) — voir ADR-0012 pour le détail complet, y compris la contrainte réseau côté Azure identique à celle déjà documentée par ADR-0010.

## 3. Droit à l'effacement

Un chemin d'effacement réel existe : `CustomerAppService.DeleteAsync`. Deux issues possibles, selon que le client a des commandes ou non (corrigé cette semaine — voir ci-dessous) :

- **Pas de commande** : suppression physique (hard delete) de la ligne `Customer`, pas de soft-delete, pas de flag `IsDeleted`. Inchangé.
- **Au moins une commande** : anonymisation (`Customer.Anonymize()`) plutôt que suppression — prénom, nom, email et adresse remplacés par des valeurs neutres, la ligne `Customer` survit. Décision : les commandes sont retenues sous l'exception d'obligation légale du RGPD (art. 17(3)(b), conservation comptable/fiscale), ce qui exige que `orders.CustomerId` continue de pointer sur une ligne réelle plutôt que de devenir orphelin.

**Une limite corrigée cette semaine, une limite réelle qui demeure :**

1. **Commandes orphelines — corrigé.** Comme noté en §1, `Order.CustomerId` n'avait pas de contrainte FK configurée : supprimer un `Customer` qui a des commandes ne les supprimait pas et ne l'empêchait pas non plus (pas de `Restrict`) — les lignes `orders` survivaient avec un `CustomerId` qui ne pointait plus sur rien. Corrigé par la contrainte FK (`ON DELETE RESTRICT`, migration `AddOrdersCustomerForeignKey`) et par la bascule anonymisation ci-dessus, qui rend ce cas impossible en usage normal — la contrainte reste comme garde-fou pour tout appelant qui contournerait `CustomerAppService`.
2. **Fenêtre de backup.** Une suppression (ou anonymisation) n'efface pas rétroactivement les backups déjà pris — la donnée reste récupérable pendant la fenêtre de rétention (jusqu'à 7 jours côté Azure, 1 jour côté AWS, §2). C'est un délai normal et généralement accepté sous RGPD (l'essentiel est que la donnée ne survive pas indéfiniment dans les backups), mais ça doit être documenté comme un fait plutôt que supposé instantané si jamais une demande d'effacement formelle arrive.

**Logs** : avant la correction de ce sprint (§4), une demande d'effacement ne touchait de toute façon pas les logs applicatifs, puisque l'email y était écrit en clair indépendamment de la ligne `Customer`/`User` en base. C'est corrigé, mais seulement pour les 8 points de log identifiés dans `AuthService` — voir §4 pour le périmètre exact.

## 4. Minimisation des logs

**Constat initial (avant correction)** : `AuthService.cs` loguait l'email en clair (`{Email}`) à 8 endroits — inscription, tentative d'inscription en doublon, connexion réussie, échec de connexion (compte inconnu, verrouillé, désactivé, mot de passe invalide), rafraîchissement de token, déconnexion. Ces logs alimentent CloudWatch/Log Analytics, conservés 30 jours (§2) — exactement le scénario que le roadmap anticipait sans l'avoir vérifié (« un identifiant client dans un log applicatif conservé 30 jours est une donnée personnelle »).

**Corrigé ce sprint** : les 8 appels remplacent `{Email}`/`user.Email` par `{UserId}`/`user.Id` (le Guid interne, pas une donnée personnelle en soi — il ne révèle rien sans accès à la base). Deux cas où aucun `User` n'existe encore au moment du log (tentative d'inscription avec un email déjà pris, tentative de connexion avec un email inconnu) : l'email est retiré du message plutôt que remplacé, le message reste informatif sans identifiant.

**Vérifié, pas supposé** : recherche exhaustive de tous les appels `_logger.Log*` dans le backend — seuls `AuthService.cs` et `ExceptionHandlingMiddleware.cs` en contiennent. Le second ne logue que le path de la requête (`context.Request.Path`), jamais de donnée personnelle. `CustomerAppService`, `OrderAppService` et les repositories associés ne loguent rien du tout — aucune fuite de PII client trouvée en dehors du périmètre auth déjà corrigé.

**Ce que ça ne couvre pas** : les logs déjà écrits avant cette correction, s'ils existent encore dans une fenêtre de rétention de 30 jours au moment du déploiement de ce fix, contiennent toujours des emails en clair. Pas d'action rétroactive possible ni nécessaire (ils expireront normalement) — mentionné ici pour ne pas prétendre à une correction plus large qu'elle ne l'est.

## 5. Résidence des données

Azure `westeurope` et AWS `eu-west-1` (Irlande) — confirmé dans `environments/dev/variables.tf` et `providers.tf`. Le commentaire du provider AWS indique que `eu-west-1` a été choisi pour la maturité des services AWS, pas pour la résidence UE — mais le résultat est conforme aujourd'hui.

`geo_redundant_backup_enabled` (Azure) et `multi_az` (AWS) sont à `false` par défaut — aucune réplication cross-région n'a lieu aujourd'hui, donc pas de risque de sortie de l'UE via un backup géo-redondant. **À vérifier explicitement si l'un de ces flags passe à `true` pour la prod** : la région pairée de `westeurope` est `northeurope` (Irlande, toujours UE) donc pas de problème prévisible, mais ça reste une contrainte à figer plutôt qu'à découvrir après coup.

## 6. Chiffrement au repos

Actif des deux côtés par défaut, sans CMK dédiée :

- **AWS RDS** : `storage_encrypted = true`, clé gérée AWS (`aws/rds`), pas de CMK — choix déjà documenté dans le code (« same "managed key is enough" call as not standing up a separate Key Vault HSM tier on the Azure side »).
- **Azure PostgreSQL Flexible Server** : chiffrement au repos actif par défaut (comportement de la plateforme, pas une option à activer), aucune `customer_managed_key` configurée.

Une clé gérée par le client (CMK/BYOK) n'est pas exigée par le RGPD en tant que tel — c'est un durcissement, pertinent seulement si un client contractuel l'exige ou si la classification évolue vers une catégorie plus sensible que « personnelle ». Pas de déclencheur identifié aujourd'hui ; noté ici pour que la question soit tranchée consciemment si elle se pose plus tard, plutôt que redécouverte.

---

## Résumé — ce qui reste réellement ouvert

**Corrigé ce sprint** : minimisation des logs d'authentification (§4). **Corrigé cette semaine** : `Order.CustomerId` sans contrainte FK — décision prise et implémentée : les commandes sont conservées sous l'exception d'obligation légale du RGPD (art. 17(3)(b), conservation comptable/fiscale), donc un effacement `Customer` qui a des commandes anonymise la ligne (`Customer.Anonymize()` — prénom/nom/email/adresse remplacés, la ligne survit) plutôt que de la supprimer ; un `Customer` sans commande reste un vrai hard delete, inchangé. Contrainte FK ajoutée à l'échelle base (`ON DELETE RESTRICT`, migration `AddOrdersCustomerForeignKey`) en garde-fou, au cas où un futur appelant contournerait `CustomerAppService.DeleteAsync`. Voir §3 et `backend/ArkCloud.Application/Services/CustomerAppService.cs`.

**Corrigé cette semaine (suite)** : purge automatisée par catégorie métier — voir ADR-0012. Seuil (3 ans), critère (dernière commande) et action (anonymisation) décidés avec l'utilisateur, pas figés unilatéralement dans le code. Implémentée en `CustomerRetentionPurgeHostedService`/`CustomerRetentionPurgeService` (Azure, in-process, vérifiée par build/tests le 12/09) et `modules/aws/gdpr-purge` (AWS, Lambda planifiée) — `terraform apply` exécuté le 12/09 des deux côtés, infrastructure provisionnée. Reste ouvert : aucune exécution réelle de la purge pas encore observée (pas de client réellement inactif depuis 3 ans en base dev à ce jour).

**À traiter, aucune décision prise à ce jour** :
- Procédure d'effacement formelle (au sens : quelqu'un fait une demande RGPD réelle) jamais testée de bout en bout, y compris le délai de survie en backup (§3).

**Acceptée / documentée, pas un chantier** :
- Asymétrie de rétention des backups AWS (1j) vs Azure (7j) — contrainte Free Tier AWS, pas un choix.
- Absence de CMK des deux côtés — pas requis par le RGPD à ce niveau de classification, à revisiter si la classification change.
- Résidence UE confirmée mais non figée en contrainte explicite au-delà de ce document — candidat pour une ADR si une exigence contractuelle formalise la contrainte.
