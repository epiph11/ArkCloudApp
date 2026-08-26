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

**Constat structurel** : `Order.CustomerId` est une simple colonne Guid, sans contrainte de clé étrangère configurée dans EF Core (`OrderConfiguration.cs` ne déclare aucun `HasOne(Customer)`). Conséquence directe pour l'effacement, voir §3.

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

**Purge automatisée** : aucune n'existe au-delà de l'expiration technique des logs/backups ci-dessus. Il n'y a pas de job qui purge une donnée personnelle *avant* sa rétention technique par catégorie métier (ex. « supprimer les clients inactifs depuis 3 ans ») — ce chantier n'est pas commencé, voir §5.

## 3. Droit à l'effacement

Un chemin d'effacement réel existe : `CustomerAppService.DeleteAsync` → suppression physique (hard delete) de la ligne `Customer`, pas de soft-delete, pas de flag `IsDeleted`.

**Deux limites réelles, non déduites — vérifiées dans le code :**

1. **Commandes orphelines.** Comme noté en §1, `Order.CustomerId` n'a pas de contrainte FK configurée. Supprimer un `Customer` qui a des commandes ne les supprime pas et ne l'empêche pas non plus (pas de `Restrict`) — les lignes `orders` survivent avec un `CustomerId` qui ne pointe plus sur rien. Ce n'est pas en soi une fuite de donnée personnelle (les champs restants — produits, montants, statut — ne sont pas nominatifs), mais ce n'est pas un effacement propre non plus : c'est un id orphelin, pas une décision de conservation assumée.
2. **Fenêtre de backup.** Une suppression n'efface pas rétroactivement les backups déjà pris — la donnée reste récupérable pendant la fenêtre de rétention (jusqu'à 7 jours côté Azure, 1 jour côté AWS, §2). C'est un délai normal et généralement accepté sous RGPD (l'essentiel est que la donnée ne survive pas indéfiniment dans les backups), mais ça doit être documenté comme un fait plutôt que supposé instantané si jamais une demande d'effacement formelle arrive.

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

**Corrigé ce sprint** : minimisation des logs d'authentification (§4).

**À traiter, aucune décision prise à ce jour** :
- Purge automatisée par catégorie métier (au-delà de l'expiration technique des backups/logs) — non commencée.
- `Order.CustomerId` sans contrainte FK — pas une fuite de PII, mais un effacement de `Customer` laisse un id orphelin plutôt qu'une décision assumée (supprimer, anonymiser, ou documenter la conservation des commandes).
- Procédure d'effacement formelle (au sens : quelqu'un fait une demande RGPD réelle) jamais testée de bout en bout, y compris le délai de survie en backup (§3).

**Acceptée / documentée, pas un chantier** :
- Asymétrie de rétention des backups AWS (1j) vs Azure (7j) — contrainte Free Tier AWS, pas un choix.
- Absence de CMK des deux côtés — pas requis par le RGPD à ce niveau de classification, à revisiter si la classification change.
- Résidence UE confirmée mais non figée en contrainte explicite au-delà de ce document — candidat pour une ADR si une exigence contractuelle formalise la contrainte.
