# ArkCloud — État des lieux final, Sprint 6 (Sécurité cloud avancée)

Rapport technique complet — 13 septembre 2026
Remplace/complète `ArkCloud-Etat-des-lieux-Sprint6.docx` (07/09/2026), qui couvrait le sprint à mi-parcours.
Sources : commits `ArkCloud` (develop `f563f62`, main aligné) et `ArkCloudInfra` (main `5a967c1`), `docs/adr/`, `docs/runbooks/`, conversation de clôture du 12-13/09/2026.

> **Note sur le format.** Ce document est en Markdown, pas `.docx` : le sandbox d'exécution (bash/npm/pandoc, nécessaire au skill de génération Word) est actuellement indisponible côté environnement (panne connue, liée à une mise à jour Windows du 8 septembre, suivie mais non résolue à ce jour). Convertible en `.docx` via Word (Ouvrir → ce fichier) ou `pandoc etat-des-lieux-sprint6-final.md -o etat-des-lieux-sprint6-final.docx` dès que le sandbox est réparé — le contenu ci-dessous est déjà complet.

---

## 1. Résumé exécutif

Le Sprint 6 est le plus long et le plus dense du projet à ce jour (démarré fin août, clos le 13/09/2026). Il fait suite à un incident réel — la fuite publique d'un fichier `tfplan` contenant l'état Terraform complet avec des secrets en clair — et s'est structuré en cinq axes :

1. **Durcissement infrastructure** : logs d'accès, chiffrement, IAM moindre-privilège, threat detection (GuardDuty, Defender for Cloud), NSG flow logs.
2. **Modélisation de menaces STRIDE** : 5 flux de confiance analysés, 3 menaces réelles trouvées et mitigées en conditions réelles (pas seulement documentées).
3. **Réduction du privilège applicatif** : rôle `arkcloud_app` (DML seul) sur les deux clouds, puis **authentification passwordless complète** (AWS IAM DB auth + Azure Entra ID) — zéro mot de passe stocké pour la connexion applicative à la base, sur les deux clouds, vérifié par un vrai test de connexion.
4. **Conformité RGPD** : classification des données, correction d'une FK orpheline, purge automatisée (rétention 3 ans).
5. **Sécurité zéro-coût** (clôture, 13/09) : chaîne d'approvisionnement logicielle (SBOM + signature d'images), puis 4 items supplémentaires le jour de la clôture — images distroless, secret-scanning pre-commit, DAST (scan dynamique), détection de drift Terraform.

**État à la clôture** : zéro menace STRIDE en attente, zéro secret en clair dans le code de connexion applicative sur les deux clouds, zéro finding High sur le DAST, backlog #102 (sécurité zéro-coût) terminé à 100%. Deux items restent ouverts et non bloquants : mise à jour finale de `docs/architecture-arkcloud.md`/CHANGELOG (ce document y contribue) et nettoyage d'une référence réseau orpheline (`snet-web`/`nsg-web`, devenue inutile depuis le partage d'un seul App Service Plan).

---

## 2. Architecture — vue d'ensemble double-cloud (état au 13/09/2026)

ArkCloud reste déployé en double-cloud actif (Azure + AWS), deux piles applicatives complètes et indépendantes. Le changement majeur depuis le rapport du 07/09 : **les deux piles s'authentifient désormais à leur base PostgreSQL sans mot de passe stocké**, via l'identité de calcul native de chaque cloud.

```
                              Utilisateur final (navigateur)
                                    |                  |
                                 HTTPS               HTTPS
                                    |                  |
        ┌───────────────────────────────┐   ┌───────────────────────────────┐
        │   AWS (eu-west-1)              │   │   Azure (westeurope)          │
        │                                 │   │                                │
        │   ALB (HTTPS, cert auto-signé) │   │   App Service Plan partagé    │
        │        |                       │   │   asp-arkcloud-dev (B1)       │
        │   ECS Fargate — web (Blazor)   │   │        |          |           │
        │        | JWT                  │   │   app-web        app-api      │
        │   ECS Fargate — api (.NET)     │   │   (Blazor)      (.NET API)    │
        │        |                       │   │                   |           │
        │   IAM DB auth (token 15 min)   │   │   Managed Identity système   │
        │   via arkcloud_app             │   │   → token AAD (Entra ID)     │
        │        |                       │   │   → arkcloud_app             │
        │   RDS PostgreSQL               │   │        |                     │
        │   psql-arkcloud-dev (privé,VPC)│   │   PostgreSQL Flexible Server │
        │                                 │   │   psql-arkcloud-dev(privé,VNet)│
        │   Secrets Manager (admin only) │   │   Key Vault (admin only)     │
        │   Lambda rotation · 90j        │   │   Function App rotation      │
        └───────────────────────────────┘   └───────────────────────────────┘
                    |                                     |
                    └──────────── GitHub Actions ─────────┘
                       (OIDC, aucune clé statique stockée)
```

Différence clé avec le schéma du 07/09 : à cette date, `arkcloud_app` existait déjà sur les deux clouds mais s'authentifiait encore par mot de passe (roté automatiquement, mais un mot de passe quand même). Depuis le 12-13/09, **plus aucun mot de passe n'existe pour ce chemin de connexion** — seul le compte admin (`arkcloudadmin`/`arkcloudadmin`) garde un mot de passe, roté automatiquement tous les 90 jours et utilisé uniquement pour les migrations/bootstrap, jamais par l'application en fonctionnement normal.

---

## 3. Chronologie du sprint (repères)

| Période | Contenu | Tâches |
|---|---|---|
| Fin août | Incident tfplan, remédiation Tier 1, début du sprint | — |
| Semaine 1 | NSG flow logs, HTTPS/ACM AWS, rotation auto Postgres (Azure+AWS), GuardDuty, Defender, audit IAM, fitness functions (ArchUnitNET, infracost) | #48-59 |
| Semaine 2 | STRIDE threat modeling (5 flux), ADR, `arkcloud_app` (DML seul, 2 clouds), rapport intermédiaire (07/09) | #60-77 |
| Semaine 2-3 | Azure Functions (réseau + module + code + apply réel), backlog Kudu, bascule réelle vers `arkcloud_app`, incident tfplan #2 (purge + rotation complète), diagnostic 503 | #78-92 |
| Semaine 3 | Drill de restauration STRIDE flux 5 (réel), passwordless AWS (IAM DB auth), SBOM/Cosign, RGPD (FK orpheline + purge auto), Renovate/Snyk/SonarCloud | #89-97 |
| 12/09 | Diagnostic CI, investigation coûts Azure (42€ vs 7-10€ cible), passwordless Azure — Terraform + C# | #98-101 |
| 12-13/09 | **Bootstrap opérationnel réel du passwordless Azure** (voir §5.2) : bug VNet/Plan, RBAC 403, bootstrap SQL, 3 bugs découverts en route (NuGet, base vide, permissions), vérification finale par un vrai test de login | #100-101, #106 |
| 13/09 | **Backlog #102 — sécurité zéro-coût** : distroless, pre-commit secret-scanning, DAST, détection de drift | #107-110 |

---

## 4. Modélisation de menaces STRIDE — bilan mis à jour

Le tableau complet (5 flux × 6 catégories) est inchangé depuis le rapport du 07/09 (voir `docs/threat-model-stride.md` pour le détail intégral). Mise à jour du bilan :

- **Résolues pendant le sprint** : logs d'accès ALB (repudiation, flux 1), privilège applicatif (élévation, flux 3), restaurabilité post-rotation (tampering, flux 5).
- **Acceptées et tracées en ADR** : certificat auto-signé (ADR-0003), `Jwt:Key` non rotatable à chaud (ADR-0004), `GHCR_PAT` secret humain (ADR-0007), absence de rate limiting infra (ADR-0008).
- **Résolu depuis le rapport du 07/09** : la fuite potentielle de donnée personnelle dans les logs applicatifs (flux 3, information disclosure), qui était « hors périmètre STRIDE, chantier RGPD séparé », est maintenant traitée — voir §6.
- **Nouveau risque neutralisé hors STRIDE strict** : élévation de privilège via mot de passe applicatif compromis — n'existe plus sur aucun des deux clouds, le mot de passe lui-même a disparu du chemin de connexion (voir §5).

Zéro menace en attente à la clôture du sprint.

---

## 5. Authentification passwordless — les deux clouds

### 5.1 AWS — IAM Database Authentication (fait avant le 12/09)

`arkcloud_app` s'authentifie via un token IAM de courte durée (15 minutes, généré par le SDK AWS au moment de la connexion) au lieu d'un mot de passe stocké. Le rôle IAM de la tâche ECS a la permission `rds-db:connect` scopée à l'utilisateur `arkcloud_app` et à l'instance RDS précise — pas de wildcard. Vérifié en conditions réelles (login réussi, `401` proprement retourné pour un utilisateur inexistant, pas de `500`).

### 5.2 Azure — Entra ID (12-13/09/2026) — bootstrap opérationnel complet

Contrairement à AWS, le scope Azure de l'ADR-0011 était resté *implémenté mais jamais opéré en réel* jusqu'au 12/09. Cette section documente le bootstrap réel, bug par bug — c'est la partie la plus dense du sprint.

#### 5.2.1 Blocage infra : changement de Plan App Service vs VNet Integration

**Erreur rencontrée** :
```
400 Bad Request: Changing App Service Plans is not allowed when Regional VNET
integration is enabled. Please disconnect from the VNET and then try again.
```

Azure refuse de changer `service_plan_id` sur un `azurerm_linux_web_app` tant que la Regional VNet Integration est active — non documenté par le provider avant d'y être confronté. **Fix** : toggle Terraform temporaire, 2 applys.

`environments/dev/variables.tf` (ArkCloudInfra) :
```hcl
# Sprint 6 clôture (12/09) — bascule temporaire, à retirer une fois le swap vers le Plan partagé
# terminé (voir tâche #106). Azure refuse de changer service_plan_id sur un azurerm_linux_web_app
# tant que la Regional VNet Integration est active ("Changing App Service Plans is not allowed
# when Regional VNET integration is enabled. Please disconnect from the VNET and then try
# again.") — trouvé en direct le 12/09/2026, aucune mention dans la doc du provider avant d'y
# être confronté. Procédure en 3 applys distincts, chacun ciblé uniquement sur les 2
# azurerm_linux_web_app :
#   1. TF_VAR_disconnect_vnet_for_plan_migration=true  -> déconnecte le VNet (service_plan_id
#      inchangé à ce stade)
#   2. TF_VAR_disconnect_vnet_for_plan_migration=false -> reconnecte le VNet, MAIS c'est cet apply
#      qui applique aussi le nouveau service_plan_id (déjà dans le state désiré depuis le premier
#      apply raté) puisque le VNet est déconnecté au moment où Azure traite le changement de Plan
#   Concrètement seuls 2 applys sont nécessaires, pas 3 — le changement de Plan et la
#   reconnexion VNet passent ensemble une fois le blocage levé à l'étape 1.
variable "disconnect_vnet_for_plan_migration" {
  description = "true = déconnecte temporairement virtual_network_subnet_id sur les 2 App Services (voir commentaire ci-dessus). Remettre à false (ou supprimer la variable) une fois le swap de Plan terminé et vérifié."
  type        = bool
  default     = false
}
```

`environments/dev/main.tf` — appliqué aux deux modules App Service :
```hcl
vnet_integration_subnet_id = var.disconnect_vnet_for_plan_migration ? null : module.network.api_subnet_id
```

Résultat : `Apply complete! Resources: 0 added, 2 changed, 0 destroyed.` Ce même swap de Plan a d'ailleurs été l'occasion de partager un seul `azurerm_service_plan` entre l'API et Blazor (économie ~12€/mois, un Plan B1 coûte le même prix pour 1 ou 2 apps dessus — Azure facture le Plan à l'heure d'existence).

#### 5.2.2 Blocage IAM : Role Definition 403

```
Error: updating Role Definition ... 403 (403 Forbidden) AuthorizationFailed
```

Le service principal CI n'avait pas de rôle `Role Based Access Control Administrator` au bon scope. **Fix** :
```powershell
az role assignment create --assignee <ciObjectId> --role "Role Based Access Control Administrator" \
  --scope "/subscriptions/<sub>/resourceGroups/rg-arkcloud-dev/providers/Microsoft.Authorization/roleDefinitions/<roleDefId>"
```
Deuxième échec identique juste après — propagation Azure AD (2-5 min), résolu en retentant.

#### 5.2.3 Bootstrap SQL — la fonction `pgaadauth_*` n'existe que sur `postgres`

```
ERROR: function pgaadauth_create_principal(unknown, boolean, boolean) does not exist
```
`\df *pgaadauth*` renvoie 0 lignes sur `arkcloud`, la liste complète sur `postgres`. Contrairement à ce que laisse entendre la documentation Microsoft, ces fonctions ne sont **pas** exposées sur chaque base.

`scripts/sql/bootstrap-arkcloud-app-entra-id.sql` (ArkCloudInfra), corrigé :
```sql
\c postgres
SELECT pgaadauth_create_principal(:'app_service_identity_name', false, false);

\c arkcloud
GRANT arkcloud_app TO %I  -- (nom de l'identité managée)
```

Résultat obtenu :
```
postgres=> SELECT pgaadauth_create_principal('app-arkcloud-api-dev', false, false);
       pgaadauth_create_principal
-----------------------------------------
 Created role for "app-arkcloud-api-dev"
(1 row)
arkcloud=> GRANT arkcloud_app TO "app-arkcloud-api-dev";
GRANT ROLE
```

Réseau : Azure Cloud Shell ne peut pas joindre Postgres (réseau Microsoft, pas dans le VNet). Contournement : console SSH Kudu de `app-arkcloud-api-dev` (dans `snet-api`), avec un token AAD récupéré séparément via Cloud Shell et collé manuellement en `PGPASSWORD`.

#### 5.2.4 Découverte : la base Azure `arkcloud` était totalement vide

```
arkcloud=> SELECT * FROM "__EFMigrationsHistory";
ERROR: relation "__EFMigrationsHistory" does not exist
```
`\dt` ne listait aucune table. Contrairement à AWS RDS (vérifié bout en bout les 10-11/09), les migrations EF Core n'avaient **jamais** été appliquées à cette instance Postgres Azure — un trou pré-existant, découvert uniquement parce que c'était le premier flux applicatif réel (login) à taper dans cette base.

**Blocage annexe** : `dotnet ef migrations script` refusait de builder —
```
error NU1605: Warning As Error: Detected package downgrade: Azure.Identity from 1.14.2 to 1.14.0
```
`ArkCloud.API.csproj` :
```xml
<!-- Aligné sur ArkCloud.Infrastructure.csproj (Sprint 6 clôture, passwordless Azure) — sinon
     NU1605 "package downgrade" bloque le restore en Warning-As-Error dès que ce projet
     référence directement une version antérieure à celle exigée transitivement. -->
<PackageReference Include="Azure.Identity" Version="1.14.2" />
```

Script généré (`migrations.sql`, 150 lignes, jamais committé — `.gitignore`), extrait :
```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (...);
START TRANSACTION;
CREATE TABLE customers (...);
CREATE TABLE orders (...);
CREATE TABLE products (...);
CREATE TABLE order_items (...);
...
CREATE TABLE roles (...);
CREATE TABLE users (...);
CREATE TABLE refresh_tokens (...);
CREATE TABLE user_roles (...);
INSERT INTO roles ("Id", "Name") VALUES ('11111111-0000-0000-0000-000000000001', 'Admin');
INSERT INTO roles ("Id", "Name") VALUES ('11111111-0000-0000-0000-000000000002', 'Manager');
INSERT INTO roles ("Id", "Name") VALUES ('11111111-0000-0000-0000-000000000003', 'User');
COMMIT;
```

**Piège de transfert** : le fichier généré par `dotnet ef migrations script` sur Windows commence par un BOM UTF-8 (`ef bb bf`), qui survit à l'aller-retour PowerShell → base64 → Kudu et casse la première instruction SQL (`syntax error at or near CREATE`). Diagnostiqué via `head -c 20 migrations.sql | od -An -tx1`. Fix : `tail -c +4 migrations.sql > migrations_clean.sql`.

#### 5.2.5 `42501: permission denied` après création des tables

Les tables venaient d'être créées par l'admin AAD (`epiphanezare@outlook.com`), pas par `arkcloudadmin` — et `ALTER DEFAULT PRIVILEGES FOR ROLE arkcloudadmin` ne s'applique qu'aux objets créés **par ce rôle précis**, pas globalement. Fix (ré-exécuté en admin) :
```sql
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO arkcloud_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO arkcloud_app;
```

#### 5.2.6 Bascule finale et vérification

`environments/dev/main.tf`, ajouté au bloc `extra_app_settings` de `module.app_service_api` :
```hcl
"Database__AuthMode" = "AzureAd"
"Database__Host"     = module.postgresql.fqdn
"Database__Port"     = "5432"
"Database__Name"     = module.postgresql.database_name
"Database__Username" = "app-arkcloud-api-dev"
```

**Preuve de fonctionnement** — requête réelle contre l'app en production dev :
```
POST /api/api/v1/auth/login  {"email":"test@test.com","password":"bidon"}
→ 401 Unauthorized {"title":"Authentication failed","status":401,"detail":"Invalid email or password."}
```
Un `401` propre (pas un `500`) prouve que l'app a bien lu la table `users` via un token Entra ID de l'identité managée système — aucune chaîne de connexion avec mot de passe n'a été utilisée. `password_auth_enabled` reste actif côté serveur PostgreSQL pour un rollback instantané (retirer `Database__AuthMode` suffit).

---

## 6. RGPD — classification, correction et purge automatisée

- **Classification des données personnelles** (`docs/rgpd-classification-donnees.md`) : entités `Customer`/`User` identifiées comme PII, vérification qu'aucune donnée personnelle ne fuite dans les logs applicatifs.
- **Correction d'une FK orpheline** : `orders.customer_id` pouvait pointer vers un client déjà anonymisé sans contrainte — ajout d'une vraie clé étrangère + logique d'anonymisation cohérente.
- **Purge automatisée** : job périodique, rétention de 3 ans après la dernière commande, anonymisation plutôt que suppression physique quand une contrainte légale l'exige. Actif uniquement côté Azure (`Gdpr__RunRetentionPurgeInProcess`), en process dans l'App Service qui a déjà l'accès réseau nécessaire — AWS n'a pas d'équivalent Lambda autonome pour cette tâche (pas de chemin réseau vers le VPC privé sans coût supplémentaire).

---

## 7. Chaîne d'approvisionnement logicielle

- **SBOM** (Syft, format SPDX) généré à chaque build d'image, publié comme artefact CI.
- **Signature d'images** (Cosign, signature "keyless" via le token OIDC de GitHub Actions — aucune clé à générer/stocker/faire tourner).
- **Attestation** : le SBOM est attaché à l'image comme attestation in-toto, récupérable directement depuis le registre (`cosign verify-attestation`).
- **Renovate** : configuration de mise à jour automatique des dépendances (`renovate.json`).
- **Snyk / SonarCloud** : scans de dépendances et qualité de code, activés en mode « no-op tant que le secret n'existe pas » (`SNYK_TOKEN`/`SONAR_TOKEN`) — gratuits en tier gratuit, pas encore connectés à un vrai compte à ce jour.

---

## 8. Coûts

- **Investigation** : dépense Azure réelle à 42€ contre une cible de 7-10€/mois — cause principale identifiée et traitée séparément (hors scope de ce document, voir tâche #99).
- **Plan App Service partagé** (12/09) : fusion des deux Plans B1 (API + Blazor) en un seul — **~12€/mois économisés**, un Plan B1 facture pareil pour 1 ou 2 apps dessus.
- **Cost-guard** : budget Azure à 7€/mois avec arrêt automatique de PostgreSQL à 100% de dépense constatée (Automation Runbook, stop-only, jamais de restart automatique par ce mécanisme).
- **Arrêt/démarrage manuel à la demande** : `dev-env-down.yml` (détruit le Plan App Service + arrête Postgres) / `dev-env-up.yml` (inverse), déclenchables à la main depuis GitHub Actions — remplace un planning fixe nuit/weekend jugé inadapté à un rythme de dev sporadique.

---

## 9. Backlog #102 — sécurité zéro-coût (clôture, 13/09/2026)

Quatre items terminés le jour de la clôture du sprint, chacun vérifié en conditions réelles (pas juste écrit et laissé de côté).

### 9.1 Images distroless

`deploy/docker/Dockerfile.blazor`, runtime basculé sur une image minimale (Ubuntu Chiseled, sans shell ni gestionnaire de paquets) :
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
```
`deploy/docker/Dockerfile.api` **volontairement laissé sur l'image standard** — exception documentée : ce Dockerfile embarque un `sshd` custom + `postgresql-client`, mécanisme Kudu utilisé pour toute la rotation/bootstrap `arkcloud_app` documentée en §5.2. Une image distroless (sans shell) casserait ce runbook.

### 9.2 Pre-commit secret-scanning

`gitleaks` via le framework `pre-commit`, sur les deux repos. Particularité ArkCloudInfra : `core.hooksPath` déjà personnalisé (`githooks/`, pour Checkov pre-push) — `pre-commit install` refusant de s'installer par-dessus, un hook `githooks/pre-commit` a été écrit à la main pour appeler l'outil directement, sans toucher à la config existante. Scan initial des deux repos : **0 secret détecté**, aucun faux positif à traiter dans `.gitleaks.toml`.

### 9.3 DAST — OWASP ZAP baseline

Nouveau workflow `dast-scan.yml` (ArkCloud), scan contre l'API et le Blazor **réellement déployés** en dev. Premier passage : **0 High** sur les deux apps. Findings Medium/Low (CSP `unsafe-inline`, en-têtes HSTS/`Permissions-Policy`/`Cross-Origin-*-Policy` absents) triés et documentés comme dette technique acceptée dans `.zap/rules.tsv` (un rule ID ZAP par ligne, `IGNORE` + justification) — gate durci en conséquence : le job échoue désormais sur tout nouveau type de finding, pas sur la récurrence des instances déjà triées.

### 9.4 Détection de drift Terraform

Nouveau workflow `drift-detection.yml` (ArkCloudInfra), `terraform plan -detailed-exitcode` hebdomadaire (lundi 8h UTC) + déclenchable à la main — lecture seule, jamais d'`apply`. Premier run réel : **aucun drift**, state conforme à la réalité. Caveat documenté dans le fichier : un run rouge pendant que l'environnement dev est éteint (`dev-env-down.yml`) n'est pas un vrai drift, c'est l'état attendu du cycle down/up.

### 9.5 Bug corollaire trouvé et corrigé pendant ce travail

`deploy-on-image.yml` (déploiement automatique déclenché à chaque publication d'image) échouait avec `No value for required variable` sur `entra_admin_principal_name`/`entra_admin_object_id` — ces deux variables avaient été ajoutées à `dev-env-up.yml`/`dev-env-down.yml`/`terraform-ci.yml` lors du passwordless Azure (§5.2) mais oubliées dans ce workflow. Corrigé (`fix(ci): ajouter TF_VAR_entra_admin_* manquantes dans deploy-on-image.yml`).

---

## 10. Registre des décisions d'architecture (ADR) — mis à jour

| ADR | Titre | Statut |
|---|---|---|
| 0003 | Certificat auto-signé sur l'ALB AWS | Acceptée (temporaire) |
| 0004 | Rotation automatique sélective des secrets | Acceptée |
| 0007 | `GHCR_PAT` — risque accepté | Acceptée |
| 0008 | Rate limiting applicatif seul | Acceptée (temporaire) |
| 0009 | Stratégie de branches et versionning | Acceptée |
| 0010 | Bootstrap/rotation `arkcloud_app` Azure — Kudu manuel | Acceptée (implémentée, voir §5.2) |
| 0011 | Authentification passwordless `arkcloud_app` | **Acceptée et implémentée — AWS et Azure, vérifiées en conditions réelles (12-13/09/2026)** |
| 0012 | Purge RGPD automatisée | Acceptée (implémentée) |
| 0013 | Parité d'environnement dev/staging/prod | Acceptée |

---

## 11. Ce qui reste ouvert

- **Nettoyage réseau** : `snet-web`/`nsg-web` (module `network`) sont définis mais plus attachés à aucune ressource depuis le partage d'un seul App Service Plan (§8) — à retirer proprement (tâche #104).
- **`docs/architecture-arkcloud.md`** : à mettre à jour pour refléter le flux d'authentification passwordless décrit en §5 (remplace les mentions de chaîne de connexion avec mot de passe) — voir aussi `CHANGELOG.md`.
- **`ALTER DEFAULT PRIVILEGES` pour l'admin AAD** : idée de backlog notée dans le runbook Azure (§5.2.5) pour éviter un re-`GRANT` manuel après une future migration lancée par un admin humain plutôt que par `arkcloudadmin` — peu fréquent en pratique (CI utilise `arkcloudadmin`), pas fait.
- **Snyk/SonarCloud** : câblés en CI mais pas encore connectés à un vrai compte/token — no-op tant que les secrets ne sont pas configurés.
- **staging/prod** : toujours non provisionnés (seul `dev` a une config Terraform réelle) — ADR-0013 pose les bases de parité, l'implémentation reste un sprint applicatif futur.
