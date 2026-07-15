# ArkCloud — Roadmap Infrastructure & DevOps

> Document de référence pour la mise en place progressive de l'infrastructure ArkCloud (Azure → AWS → Kubernetes → SRE). Basé sur la doc originale fournie, mise à jour avec les décisions prises en session : séparation des repos, emplacement de Terraform, positionnement de JFrog/Jenkins, et exclusion de Puppet/Chef.

---

## 📌 Journal des décisions (par rapport à la doc originale)

| Sujet | Doc originale | Décision retenue |
|---|---|---|
| Structure du code applicatif | `src/ArkCloud.API`, `src/ArkCloud.Blazor`, etc. | **Inchangé : `backend/` + `frontend/`** — la restructuration a déjà eu lieu, tous les chemins CI/`.sln`/Dockerfiles/docker-compose y sont câblés. Repartir sur `src/` serait un renommage sans valeur ajoutée. |
| Emplacement Terraform | `ArkCloud/deploy/terraform/` (monorepo) | **Repo séparé `mon-projet-infra`**, avec la même structure de modules/environnements que celle proposée. CI/CD et permissions restent distincts du repo applicatif (blast radius, gouvernance des changements infra vs code). |
| Registre d'images/packages | Non spécifié (implicitement ACR/ECR) | **JFrog Artifactory**, introduit en fin de Sprint 4 / début Sprint 5, comme registre unique multi-cloud (remplace GHCR et évite de dupliquer ACR + ECR). Swap de configuration CI, pas de refonte de code. |
| Jenkins | Non mentionné | Évalué **en parallèle de GitHub Actions à partir du Sprint 9** (Kubernetes) — pas avant, tant que les cibles restent PaaS/serverless (App Service, ECS Fargate) où GH Actions suffit. Un seul pipeline porté en test, sans rien couper côté GH Actions. |
| Puppet / Chef | Non mentionné | **Écartés du roadmap.** Aucune VM longue durée à maintenir dans ce plan (App Service = PaaS, ECS Fargate = serverless, EKS/AKS = nodes managés) : pas de terrain d'usage réel pour un outil de config management. |
| Kubernetes | Step 9, testable localement avant AKS/EKS | Confirmé en **Sprint 9**, après les fondations Azure (Sprint 4) et AWS (Sprint 5). `deploy/kubernetes/` reste un dossier vide (placeholder) jusque-là. |

---

## 🗺️ Correspondance avec les sprints

| Sprint | Contenu | Statut |
|---|---|---|
| 1 | Cadrage + backend de base | ✅ Fait |
| 2 | Qualité backend (validation, middleware, logging, tests, docker compose) | ✅ Fait |
| 3 | Auth JWT + Blazor | ✅ Fait |
| 4 | **CI/CD + Azure** (ce document, Steps 1–9 partie Azure) | 🔄 En cours |
| 5 | AWS foundation (Steps 10–12) | ⏳ À venir |
| 6 | Sécurité cloud avancée (Step 16) | ⏳ À venir |
| 7 | Angular enterprise | ⏳ À venir |
| 8 | Microservices | ⏳ À venir |
| 9 | Kubernetes (Step 9 pour de vrai, sur cluster managé) | ⏳ À venir |
| 10 | SRE / plateforme | ⏳ À venir |

---

## Step 1 — Organisation du repository

**Structure actuelle (réelle, pas celle de la doc d'origine) :**

```
ArkCloud/
│
├── .github/workflows/
│   ├── backend-ci.yml
│   └── frontend-ci.yml
│
├── deploy/
│   ├── docker/
│   ├── kubernetes/        (placeholder, vide jusqu'au Sprint 9)
│   └── seed/               (seed_users.sql, seed_catalog_and_orders.sql)
│
├── docs/
│
├── backend/
│   ├── ArkCloud.API/
│   ├── ArkCloud.Application/
│   ├── ArkCloud.Domain/
│   ├── ArkCloud.Infrastructure/
│   └── tests/
│
├── frontend/
│   ├── ArkCloud.Blazor/
│   └── ArkCloud.Tests.Component/
│
├── ArkCloud.sln
├── README.md
└── .gitignore
```

> **Objectif inchangé** : faire évoluer Infrastructure, Applications et DevOps indépendamment. Sauf que "Infrastructure" (Terraform) vit dans son propre repo plutôt que sous `deploy/`.

**Structure cible pour `mon-projet-infra` (repo séparé) :**

```
mon-projet-infra/
│
├── .github/workflows/
│   └── terraform-ci.yml       (existe déjà — plan sur PR, apply sur merge, gate manuel prod)
│
├── modules/
│   ├── azure/
│   │   ├── resource-group/
│   │   ├── network/
│   │   ├── app-service/
│   │   ├── postgresql/
│   │   ├── key-vault/
│   │   ├── monitoring/
│   │   └── identity/
│   │
│   ├── aws/                    (dossiers créés vides, remplis au Sprint 5)
│   │   ├── vpc/
│   │   ├── security/
│   │   ├── ecr/
│   │   ├── ecs/
│   │   ├── alb/
│   │   ├── rds/
│   │   ├── secrets/
│   │   └── monitoring/
│   │
│   └── shared/
│
├── environments/
│   ├── dev/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   ├── outputs.tf
│   │   ├── backend.tf
│   │   └── terraform.tfvars.example
│   ├── staging/                (même structure)
│   └── prod/                   (même structure)
│
├── README.md
└── .gitignore
```

> Chaque dossier d'environnement est un root module Terraform autonome (son propre state, son propre `backend.tf`) qui référence les modules via chemin relatif — pas de "racine" partagée entre states séparés.

---

## Step 2 — Outillage infrastructure à installer (poste local)

**À installer :**

- Terraform
- Azure CLI
- AWS CLI *(Sprint 5)*
- Docker Desktop
- kubectl *(Sprint 9)*
- Helm *(Sprint 9)*
- Extension VS Code Terraform
- tflint
- Checkov
- Trivy

**Vérification :**

```bash
terraform version
az version
aws --version
docker version
kubectl version --client
helm version
```

> ⚠️ Cette étape est côté poste local uniquement — l'environnement sandbox utilisé pour générer le code n'a ni Azure CLI, ni AWS CLI, ni accès réseau vers Azure/AWS. Tout ce qui suit (`terraform init/plan/apply`, `az login`, déploiements réels) doit être exécuté localement, résultats à remonter pour debug.

---

## Step 3 — Configuration Terraform (racine par environnement)

Dans `mon-projet-infra/environments/<env>/`, chaque environnement contient :

```
versions.tf
providers.tf
variables.tf
outputs.tf
```

**Providers configurés :**

- Terraform (version pin)
- Azure Provider (`azurerm`)
- AWS Provider (`aws`) *(Sprint 5)*
- Random Provider
- TLS Provider

---

## Step 4 — Remote Terraform State (Azure)

**À créer manuellement (bootstrap, one-shot — pas du Terraform, chicken-and-egg classique) :**

- Resource Group : `rg-terraform-state`
- Storage Account : `arkcloudtfstate`
- Blob Container : `terraform`

**Backend configuré via :**

```
terraform.tfstate
```

**À activer :**

- [ ] Versioning
- [ ] Soft Delete
- [ ] Blob Locking

---

## Step 5 — Modules Terraform réutilisables

Plutôt qu'un unique gros projet Terraform, des modules réutilisables par environnement.

### Azure — `modules/azure/`

- Resource Group
- Virtual Network
- Subnets
- Network Security Groups
- App Service Plan
- Linux Web App
- PostgreSQL Flexible Server
- Key Vault
- Managed Identity
- Application Insights
- Log Analytics

### AWS — `modules/aws/` *(Sprint 5)*

- VPC
- Public Subnets
- Private Subnets
- Route Tables
- Internet Gateway
- NAT Gateway
- Security Groups
- ECR
- ECS Cluster
- ECS Service
- Application Load Balancer
- Target Groups
- RDS PostgreSQL
- Secrets Manager
- IAM
- CloudWatch
- CloudTrail

---

## Step 6 — Environnements Terraform

```
environments/
├── dev/
├── staging/
└── prod/
```

Chacun contient `main.tf`, `terraform.tfvars`, `variables.tf`, `outputs.tf`.

```bash
terraform apply -var-file=dev.tfvars
```

---

## Step 7 — Déploiement de la fondation Azure

Déployer dans cet ordre :

### 7.1 Resource Group
`rg-arkcloud-dev`

### 7.2 Virtual Network
`10.10.0.0/16`, avec subnets :
- Application : `10.10.1.0/24`
- Database : `10.10.2.0/24`
- Private Endpoint : `10.10.3.0/24`

### 7.3 Network Security Groups
- Application : autoriser HTTPS
- Database : autoriser PostgreSQL
- Tout le reste : refusé

### 7.4 PostgreSQL Flexible Server
- PostgreSQL 16
- Zone Redundant *(prod uniquement)*
- Backup Retention
- Automatic Patching
- Private Access
- SSL Enforcement
- Storage Auto Grow

### 7.5 Azure Key Vault
Stocke : mot de passe base de données, secret JWT, clés API, clés Azure Storage.
Activer : RBAC, Soft Delete, Purge Protection.

> Continuité directe avec le pattern Key Vault déjà codé côté ArkCloud (`Program.cs`, no-op tant que `KeyVault:Uri` n'est pas configuré) — ici on provisionne enfin le vault réel derrière ce pattern.

### 7.6 Managed Identity
System Assigned Identity, rôle **Key Vault Secrets User** — l'API récupère les secrets automatiquement, sans identifiant en dur.

### 7.7 App Service Plan
Linux, Always On, HTTPS Only, TLS 1.2.

### 7.8 Déploiement d'ArkCloud.API
Publier `backend/ArkCloud.API`. Configurer : variables d'environnement, health check, logging, Application Insights.

> Variable de registre d'image prévue dès maintenant (GHCR par défaut) pour permettre le swap vers **JFrog Artifactory** sans modifier le module.

### 7.9 Monitoring
Application Insights + Log Analytics Workspace + Diagnostic Settings.
Suivre : requests, exceptions, dependencies, availability tests.

---

## Step 8 — Dockerisation d'ArkCloud

Dans `ArkCloud/deploy/docker/` :

```
Dockerfile.api
Dockerfile.blazor
docker-compose.yml
docker-compose.override.yml
```

Conteneuriser `ArkCloud.API` et `ArkCloud.Blazor` avec : build multi-stage, utilisateur non-root, healthcheck, variables d'environnement.

> Les Dockerfiles vivent actuellement dans `backend/ArkCloud.API/` et `frontend/ArkCloud.Blazor/` (référencés depuis `docker-compose.yml`). Les déplacer sous `deploy/docker/` avec des noms explicites (`Dockerfile.api`/`Dockerfile.blazor`) est une amélioration à faire dans le cadre de ce sprint.

---

## Step 9 — Manifests Kubernetes *(Sprint 9 — pas maintenant)*

Dans `ArkCloud/deploy/kubernetes/` :

```
namespace.yaml
deployment-api.yaml
service-api.yaml
deployment-blazor.yaml
service-blazor.yaml
configmap.yaml
secret.yaml
ingress.yaml
hpa.yaml
```

Testable localement (Docker Desktop Kubernetes / Minikube) avant de viser AKS/EKS.

> Reporté au Sprint 9 par choix — tant que les cibles de déploiement sont App Service (Sprint 4) et ECS Fargate (Sprint 5), un cluster Kubernetes n'apporte rien. `deploy/kubernetes/` reste un dossier vide (`.gitkeep`) jusque-là.

---

## Step 10 — Réseau AWS *(Sprint 5)*

VPC `10.0.0.0/16` :
- Public Subnets : `10.0.1.0/24`, `10.0.2.0/24`
- Private ECS : `10.0.11.0/24`, `10.0.12.0/24`
- Private Database : `10.0.21.0/24`, `10.0.22.0/24`

Déployer : Internet Gateway, NAT Gateway, Route Tables, Security Groups, VPC Endpoints.

---

## Step 11 — Déploiement des conteneurs ArkCloud sur AWS *(Sprint 5)*

```
Amazon ECR
   ↓
Push Docker Images
   ↓
ECS Cluster (Fargate)
   ↓
Task Definition
   ↓
ECS Service
   ↓
Application Load Balancer
   ↓
HTTPS
   ↓
Health Checks
```

Activer : CloudWatch Logs, Container Insights.

> Réutilise le registre JFrog introduit en Sprint 4 plutôt que de dupliquer avec ECR — à valider selon coûts/latence réels une fois en place.

---

## Step 12 — Amazon RDS *(Sprint 5)*

PostgreSQL, configuré avec : Private Subnets, Multi-AZ, chiffrement KMS, backups automatiques, Performance Insights.
Security Group : accès ECS uniquement.

---

## Step 13 — Gestion des secrets

```
Azure                          AWS
Key Vault                      Secrets Manager
   ↓                              ↓
Managed Identity                IAM Task Role
   ↓                              ↓
ArkCloud.API                    ArkCloud.API
```

**Ne jamais placer de secret dans :** `appsettings.json`, les images Docker, le repository Git.

> Déjà respecté côté ArkCloud : `appsettings.Development.json` ne contient plus de clé JWT réelle depuis le pattern Key Vault (Sprint 3), `user-secrets` en dev, `.env` gitignored pour Docker Compose.

---

## Step 14 — CI/CD

Dans `.github/workflows/` (réparti entre les deux repos, vu la séparation ArkCloud / mon-projet-infra) :

**Côté ArkCloud :**
```
backend-ci.yml       (existe — restore/build/test/publish/push image)
frontend-ci.yml      (existe)
```
À enrichir : scan Trivy de l'image avant push, déclenchement cross-repo vers `mon-projet-infra` (ex. `repository_dispatch`) pour lancer le déploiement du nouveau tag d'image.

**Côté mon-projet-infra :**
```
terraform-ci.yml     (existe — à enrichir avec tflint + checkov + terraform plan/PR, apply/merge gated)
```

**Pipeline :**

```
Développeur → Git Push → Restore → Build → Tests unitaires
   → Docker Build → Scan Trivy → Push Image
   → Terraform Validate → Terraform Plan → Terraform Apply
   → Déploiement → Smoke Tests → Monitoring
```

---

## Step 15 — Monitoring & Observabilité

**Azure :** Application Insights, Log Analytics.
**AWS** *(Sprint 5)* : CloudWatch Logs/Metrics/Dashboards, CloudTrail, GuardDuty.

**À surveiller :** requêtes HTTP, exceptions, CPU, mémoire, connexions base de données, redémarrages de conteneurs, échecs de déploiement, événements de sécurité.

---

## Step 16 — Durcissement sécurité

**Azure :** Managed Identity, Key Vault RBAC, Private Endpoints, NSGs, Defender for Cloud *(optionnel)*.
**AWS** *(Sprint 5-6)* : IAM least privilege, Security Groups, Secrets Manager, CloudTrail, AWS Config, GuardDuty, Security Hub *(optionnel)*.
**Application** *(déjà en place côté ArkCloud)* : HTTPS Only, JWT Authentication, ASP.NET Core Data Protection, CORS Policies, Rate Limiting, aucun secret en log.

> Puppet/Chef volontairement absents de cette section — pas de VM longue durée dans ce roadmap à faire converger/dériver.

---

## Step 17 — Checklist de préparation production

**Infrastructure**
- [ ] Remote Terraform State
- [ ] Terraform modulaire
- [ ] Environnements Dev / Staging / Prod
- [ ] Convention de nommage
- [ ] Tags de ressources

**Azure**
- [ ] Resource Group
- [ ] VNet & NSGs
- [ ] PostgreSQL Flexible Server
- [ ] Key Vault
- [ ] Managed Identity
- [ ] App Service
- [ ] Application Insights

**AWS** *(Sprint 5)*
- [ ] VPC
- [ ] Subnets publics & privés
- [ ] NAT Gateway
- [ ] ECR
- [ ] ECS Fargate
- [ ] ALB
- [ ] RDS PostgreSQL
- [ ] Secrets Manager
- [ ] CloudWatch

**DevOps**
- [ ] Images Docker
- [ ] Manifests Kubernetes *(Sprint 9)*
- [ ] GitHub Actions
- [ ] Automatisation Terraform
- [ ] Tests automatisés
- [ ] Scan de sécurité (Trivy/Checkov)
- [ ] Registre JFrog Artifactory
- [ ] Évaluation Jenkins *(Sprint 9)*

**Sécurité**
- [ ] IAM/RBAC least privilege
- [ ] Chiffrement au repos (KMS / Key Vault)
- [ ] TLS partout
- [ ] Rotation des secrets
- [ ] Audit logging activé

---

*Ce roadmap garde l'application (`ArkCloud.API`, `ArkCloud.Blazor`, `ArkCloud.Application`, `ArkCloud.Domain`, `ArkCloud.Infrastructure`) séparée des assets de déploiement (`deploy/docker`, `deploy/kubernetes`) dans le repo ArkCloud, et de l'infrastructure cloud (Terraform) dans `mon-projet-infra`. Il laisse la place pour évoluer d'App Service → AKS/EKS, Docker → Kubernetes, et single-cloud → multi-cloud sans avoir à restructurer les repos plus tard.*
