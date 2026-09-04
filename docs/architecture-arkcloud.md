# Architecture ArkCloud — document de référence

> Document de référence technique, du plus général (vue d'ensemble) au plus spécifique (chaque flux, chaque module Terraform, chaque table). Généré à partir de l'état réel du code et de l'infrastructure au 27/08/2026 — pas un document théorique, chaque affirmation ici est vérifiable dans le repo (`ArkCloud` et `ArkCloudInfra`) ou dans les ADR/roadmap qui l'accompagnent.

---

## 1. Historique du projet

| Sprint | Contenu | Statut |
|---|---|---|
| 1 | Cadrage, backend .NET 10 (Domain / Application / Infrastructure / API), scaffold initial | ✅ Clôturé |
| 2 | Qualité backend — tests unitaires, conventions de code | ✅ Clôturé |
| 3 | Authentification JWT, frontend Blazor Server | ✅ Clôturé |
| 4 | CI/CD GitHub Actions + infrastructure Azure (App Service, PostgreSQL Flexible Server, Key Vault, Application Insights) | ✅ Clôturé (28/07/2026) |
| 5 | Infrastructure AWS en parallèle (VPC, ECS Fargate, RDS PostgreSQL, ALB, CloudTrail, monitoring) — double-cloud actif | ✅ Clôturé |
| 6 | Sécurité cloud avancée : NSG flow logs, HTTPS/ACM, rotation automatique des secrets, GuardDuty, Defender for Cloud, audit IAM, fitness functions (ArchUnitNET, infracost), STRIDE, RGPD, ADR, SBOM, versionning | 🔄 En cours |
| 7 | Angular enterprise | ⏳ À venir |
| 8 | Microservices, Kafka, résilience, traçabilité distribuée | ⏳ À venir |
| 9 | Kubernetes (AKS/EKS) | ⏳ À venir |
| 10 | SRE / plateforme, chaos engineering | ⏳ À venir |
| 11 | Adoption TOGAF 10.0 | ⏳ À venir |

Décision structurante prise en Sprint 5 (ADR-0005, migration non commencée) : ArkCloud vise une architecture cible **primaire + DR** répartie sur deux clouds plutôt qu'un seul — c'est pour ça que Azure et AWS hébergent chacun une pile applicative complète et indépendante depuis le Sprint 5, pas juste un environnement de test parallèle.

Versionning (ADR-0009, Sprint 6) : modèle trunk-based allégé — `develop` reste la branche d'intégration continue, `main` est protégée (PR + CI obligatoires) et n'est mise à jour qu'à la clôture d'un sprint ou d'un jalon stable. SemVer démarré à `v0.1.0`, `0.x` signifiant explicitement l'absence de garantie de stabilité à ce stade.

---

## 2. Vue d'ensemble (contexte système)

```mermaid
graph TB
    User["Utilisateur final<br/>(navigateur)"]
    Dev["Équipe de développement"]

    subgraph ArkCloud["Système ArkCloud"]
        App["Application e-commerce B2B<br/>(clients, commandes, produits)"]
    end

    GH["GitHub<br/>(code source, CI/CD, registre GHCR)"]
    Azure["Microsoft Azure<br/>(West Europe)"]
    AWS["Amazon Web Services<br/>(eu-west-1)"]

    User -->|HTTPS| App
    Dev -->|push / PR| GH
    GH -->|déploie| Azure
    GH -->|déploie| AWS
    App -.->|hébergée sur| Azure
    App -.->|hébergée sur| AWS

    style ArkCloud fill:#F1EFE8,stroke:#5F5E5A
```

ArkCloud est une application e-commerce B2B (gestion clients/commandes/produits) volontairement déployée en double-cloud actif — pas un site principal + un site de secours passif, mais deux piles applicatives complètes et indépendantes (Azure et AWS), chacune avec son propre frontend, API, base de données et secrets. L'objectif pédagogique et technique du projet est de couvrir l'éventail complet d'un système d'entreprise réel : sécurité, conformité, observabilité, gouvernance, jusqu'aux microservices et à Kubernetes dans les sprints à venir.

---

## 3. Vue conteneurs — les deux piles cloud

```mermaid
graph TB
    Browser["Navigateur"]

    subgraph AzureCloud["Azure — West Europe (rg-arkcloud-dev)"]
        AzWeb["App Service — ArkCloud.Blazor<br/>app-arkcloud-web-dev"]
        AzApi["App Service — ArkCloud.API<br/>app-arkcloud-api-dev"]
        AzDb["PostgreSQL Flexible Server<br/>psql-arkcloud-dev"]
        AzKv["Key Vault<br/>kv-arkcloud-dev"]
        AzMon["Application Insights +<br/>Log Analytics"]
        AzRot["Automation Runbook<br/>rotation 90j"]
        AzDef["Microsoft Defender<br/>for Cloud"]
    end

    subgraph AwsCloud["AWS — eu-west-1"]
        AwsAlb["ALB<br/>alb-arkcloud-dev"]
        AwsEcsApi["ECS Fargate — API<br/>task arkcloud-arkcloud-dev-api"]
        AwsEcsWeb["ECS Fargate — Web<br/>task arkcloud-arkcloud-dev-web"]
        AwsDb["RDS PostgreSQL<br/>psql-arkcloud-dev"]
        AwsSm["Secrets Manager"]
        AwsCw["CloudWatch +<br/>CloudTrail"]
        AwsRot["Lambda rotation<br/>90j"]
        AwsGd["GuardDuty"]
    end

    GHA["GitHub Actions<br/>(CI/CD, OIDC)"]

    Browser -->|HTTPS, cert Microsoft| AzWeb
    Browser -->|HTTPS, cert auto-signé| AwsAlb
    AzWeb -->|JWT bearer| AzApi
    AwsAlb -->|/api/*| AwsEcsApi
    AwsAlb -->|défaut| AwsEcsWeb
    AwsEcsWeb -->|JWT bearer| AwsEcsApi
    AzApi -->|arkcloudadmin| AzDb
    AwsEcsApi -->|arkcloudadmin| AwsDb
    AzApi -.->|lit secrets| AzKv
    AwsEcsApi -.->|lit secrets| AwsSm
    AzRot -->|change mdp + réécrit| AzKv
    AzRot -->|redémarre| AzApi
    AwsRot -->|change mdp + réécrit| AwsSm
    AwsRot -->|redéploie| AwsEcsApi
    GHA -->|OIDC, terraform apply| AzureCloud
    GHA -->|OIDC, terraform apply| AwsCloud
    GHA -->|push image| AwsEcsApi
    GHA -->|push image| AzApi

    style AzureCloud fill:#EEEDFE,stroke:#534AB7
    style AwsCloud fill:#FAECE7,stroke:#993C1D
```

Point notable, volontaire : les deux clouds ne sont pas symétriques dans leur mécanique interne (Azure utilise un Automation Runbook PowerShell, AWS une Lambda Python custom pour la rotation des secrets ; Azure route par hostname séparé, AWS par path-based routing sur un seul ALB) — même politique de sécurité, mécanismes différents parce que les plateformes n'offrent pas les mêmes primitives natives.

---

## 4. Inventaire des modules Terraform (`ArkCloudInfra`)

| Module | Cloud | Rôle |
|---|---|---|
| `resource_group` | Azure | Groupe de ressources `rg-arkcloud-dev` |
| `network` | Azure | VNet, subnets, NSG |
| `postgresql` | Azure | PostgreSQL Flexible Server |
| `key_vault` | Azure | Coffre de secrets (RBAC, purge protection) |
| `monitoring` | Azure | Application Insights, Log Analytics, diagnostic settings |
| `app_service_api` / `app_service_web` | Azure | App Services (API, Blazor) |
| `keyvault_access_api` | Azure | Accès RBAC de l'API au Key Vault |
| `azure_cost_guard` | Azure | Garde-fou budget (7 €/mois), arrêt auto Postgres |
| `azure_secret_rotation` | Azure | Automation Runbook, rotation Postgres 90j |
| `flow_logs` | Azure | NSG flow logs → Storage Account |
| `azure_defender` | Azure | Microsoft Defender for Cloud (Key Vault actif par défaut) |
| `aws_vpc` | AWS | VPC, subnets publics/privés, NAT Gateway |
| `aws_security` | AWS | Security groups (ALB, ECS, base) |
| `aws_rds` | AWS | RDS PostgreSQL |
| `aws_secrets` | AWS | Secrets Manager (JWT, connexion DB) |
| `aws_ecr` | AWS | Registre d'images (provisoire, ADR-0002) |
| `aws_ecs` | AWS | Cluster ECS Fargate |
| `aws_alb` | AWS | Application Load Balancer, TLS, **logs d'accès S3 (Sprint 6)** |
| `aws_ecs_service_api` / `aws_ecs_service_web` | AWS | Services ECS (API, Web) |
| `aws_cloudtrail` | AWS | Audit trail, bucket S3 dédié |
| `aws_monitoring` | AWS | CloudWatch, alarmes, dashboard, SNS |
| `aws_secret_rotation` | AWS | Lambda custom, rotation Postgres 90j |
| `aws_guardduty` | AWS | Détection de menaces (compte/région) |

24 modules au total, répartis en deux environnements indépendants (`environments/dev`, un seul à ce jour — staging/prod prévus Sprints 9-10) mais un seul state Terraform partagé entre les deux clouds.

---

## 5. Flux détaillés (modélisation STRIDE, Sprint 6)

Chaque flux ci-dessous correspond à une ligne du threat model (`docs/threat-model-stride.md`) — la séquence complète d'une requête, de bout en bout, avec les frontières de confiance traversées.

```mermaid
sequenceDiagram
    actor U as Navigateur
    participant ALB as ALB (AWS)
    participant AZ as App Service (Azure)
    participant WEB as Blazor Web
    participant API as API
    participant DB as PostgreSQL
    participant S3 as S3 access logs

    U->>ALB: HTTPS (cert auto-signé, ADR-0003)
    ALB-->>S3: log d'accès (Sprint 6, STRIDE)
    ALB->>WEB: route par défaut
    U->>AZ: HTTPS (cert Microsoft)
    AZ->>WEB: route directe
    WEB->>API: JWT bearer token
    API->>API: policy d'autorisation par rôle
    API->>DB: connexion arkcloudadmin (à corriger, item STRIDE en cours)
    DB-->>API: résultat requête
    API-->>WEB: réponse
    WEB-->>U: page rendue
```

```mermaid
sequenceDiagram
    participant GH as GitHub Actions
    participant OIDC as Azure AD / AWS STS
    participant TF as Terraform apply
    participant AZ as Azure
    participant AWS as AWS
    participant GHCR as GHCR / ECR

    GH->>OIDC: jeton OIDC (pas de clé statique)
    OIDC-->>GH: identité temporaire scopée au repo
    GH->>GHCR: push image (API, Web)
    GH->>TF: plan (PR) puis apply (push main)
    TF->>AZ: crée/modifie ressources
    TF->>AWS: crée/modifie ressources
    Note over TF: apply gated par l'Environment<br/>GitHub "production"
```

```mermaid
sequenceDiagram
    participant R as Rotation (Runbook / Lambda)
    participant DB as PostgreSQL
    participant SEC as Key Vault / Secrets Manager
    participant APP as App Service / ECS

    R->>DB: change le mot de passe
    R->>DB: teste la nouvelle connexion (AWS uniquement)
    alt test réussi
        R->>SEC: promeut le nouveau secret
        R->>APP: redémarre / redéploie
    else test échoué
        R--xR: rien n'est promu, ancien mot de passe reste actif
    end
```

### Résumé STRIDE — état réel au 27/08/2026

| Flux | Menace | État |
|---|---|---|
| 1. Navigateur → ALB/App Service | Certificat auto-signé (AWS) | Acceptée — ADR-0003 |
| 1. Navigateur → ALB/App Service | Logs d'accès ALB absents | **Mitigée (Sprint 6)** |
| 1. Navigateur → ALB/App Service | Pas de rate limiting infra | Acceptée pour l'instant — ADR-0008 |
| 3. API → base | Compte applicatif trop privilégié | **En cours de correction** (rôle `arkcloud_app`) |
| 3. API → base | Données personnelles dans les logs | Mitigée (Sprint 6, `AuthService.cs`) |
| 4. CI → cloud | `GHCR_PAT`, secret humain | Acceptée — ADR-0007, rotation automatisée depuis Sprint 6 |
| 5. Rotation → base | Restaurabilité après rotation jamais testée | À traiter |

---

## 6. Pipelines CI/CD

```mermaid
graph LR
    subgraph ArkCloudRepo["Repo ArkCloud"]
        BCI["arkcloud-backend-ci.yml<br/>build, test, fitness functions"]
        FCI["arkcloud-frontend-ci.yml"]
    end

    subgraph InfraRepo["Repo ArkCloudInfra"]
        TCI["terraform-ci.yml<br/>lint, scan, plan, apply"]
        DOI["deploy-on-image.yml<br/>apply ciblé (-target)"]
        SEC["secret-expiry-check.yml<br/>cron quotidien"]
    end

    BCI -->|push image| GHCR[("GHCR")]
    BCI -->|repository_dispatch| DOI
    BCI -->|NDepend, ArchUnitNET| Quality["Quality gates"]
    TCI -->|push main ou workflow_dispatch| Apply["terraform apply"]
    DOI -->|-target app_service / ecs_service| Apply
    SEC -->|lit| Inventory[(".github/secrets-inventory.json")]
```

`terraform-ci.yml` a été modifié au Sprint 6 pour que `workflow_dispatch` puisse aussi déclencher un vrai `apply` (auparavant limité au `plan`) — nécessaire pour que la rotation automatisée de `GHCR_PAT` (`scripts/rotate-ghcr-pat.ps1`) puisse réellement repropager le secret sans attendre un push sur `main`.

---

## 7. Modèle de données

```mermaid
erDiagram
    CUSTOMERS ||--o{ ORDERS : passe
    ORDERS ||--o{ ORDER_ITEMS : contient
    PRODUCTS ||--o{ ORDER_ITEMS : référence
    USERS ||--o{ USER_ROLES : a
    ROLES ||--o{ USER_ROLES : assigné
    USERS ||--o{ REFRESH_TOKENS : possède

    CUSTOMERS {
        uuid id PK
        string first_name
        string last_name
        string email
        string street
        string city
        string country
    }
    ORDERS {
        uuid id PK
        uuid customer_id FK
        int status
    }
    ORDER_ITEMS {
        uuid id PK
        uuid order_id FK
        uuid product_id FK
        int quantity
        decimal unit_price
    }
    PRODUCTS {
        uuid id PK
        string name
        string sku
    }
    USERS {
        uuid id PK
        string email
    }
    ROLES {
        uuid id PK
        string name
    }
    USER_ROLES {
        uuid user_id FK
        uuid role_id FK
    }
    REFRESH_TOKENS {
        uuid id PK
        uuid user_id FK
        string token
    }
```

Issu de 4 migrations EF Core réelles (`InitialCreate`, `AddAuthentication`, `AddCreatedAtTimestamps`, `AddProductStockAndCategory`). Point ouvert, identifié pendant le chantier RGPD : `orders.customer_id` reste orphelin quand un client est supprimé (droit à l'effacement incomplet, `docs/rgpd-classification-donnees.md`).

---

## 8. Gouvernance et décisions d'architecture

| ADR | Décision | Statut |
|---|---|---|
| 0001 | Terraform dans un repo séparé (`ArkCloudInfra`) | Acceptée |
| 0002 | Amazon ECR provisoire plutôt que JFrog | Acceptée |
| 0003 | Certificat auto-signé sur l'ALB AWS | Acceptée (temporaire) |
| 0004 | Rotation automatique sélective des secrets | Acceptée |
| 0005 | Architecture cible Azure/AWS — primaire + DR | Acceptée (migration non commencée) |
| 0007 | `GHCR_PAT` — risque accepté | Acceptée |
| 0008 | Rate limiting applicatif seul, rien en infra | Acceptée (temporaire) |
| 0009 | Stratégie de branches et de versionning | Acceptée |

*(0006 réservée — la rotation `Jwt:Key` est couverte par l'ADR-0004, le numéro n'est pas réattribué.)*

Outillage qualité/architecture (Sprint 6, en cours) : ArchUnitNET (fitness functions de couche, en CI), infracost (garde-fou coût sur les plans Terraform), essai NDepend (28 jours, ne sera pas conservé au-delà — un faux positif connu du style top-level statements y a été trouvé et documenté plutôt que corrigé). SonarQube/SonarCloud, Snyk et Renovate restent à mettre en place, un par un.

---

## 9. Prochaines étapes (Sprint 6, reste à faire)

- Clôturer les 2 items STRIDE restants : revue des privilèges SQL (`arkcloud_app`, en cours), drill de restauration après rotation.
- SBOM (Syft) + signature d'images (Cosign).
- Purge automatique après la fenêtre de rétention RGPD, correction de `orders.customer_id` orphelin.
- SonarCloud, Snyk, Renovate.
- Merge `develop` → `main` + tag SemVer à la clôture du sprint.
