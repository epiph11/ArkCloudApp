# ArkCloud — Roadmap Infrastructure & DevOps

> Document de référence pour la mise en place progressive de l'infrastructure ArkCloud (Azure → AWS → Kubernetes → SRE). Basé sur la doc originale fournie, mise à jour avec les décisions prises en session : séparation des repos, emplacement de Terraform, positionnement de JFrog/Jenkins, et exclusion de Puppet/Chef.

---

## 📌 Journal des décisions (par rapport à la doc originale)

| Sujet | Doc originale | Décision retenue |
|---|---|---|
| Structure du code applicatif | `src/ArkCloud.API`, `src/ArkCloud.Blazor`, etc. | **Inchangé : `backend/` + `frontend/`** — la restructuration a déjà eu lieu, tous les chemins CI/`.sln`/Dockerfiles/docker-compose y sont câblés. Repartir sur `src/` serait un renommage sans valeur ajoutée. |
| Emplacement Terraform | `ArkCloud/deploy/terraform/` (monorepo) | **Repo séparé `ArkCloudInfra`**, avec la même structure de modules/environnements que celle proposée. CI/CD et permissions restent distincts du repo applicatif (blast radius, gouvernance des changements infra vs code). |
| Registre d'images/packages | Non spécifié (implicitement ACR/ECR) | **Révisé en Sprint 5 (task #33)** : JFrog Artifactory n'a finalement pas été introduit en Sprint 4/5 comme prévu ici initialement — décision prise en session de partir sur **Amazon ECR provisoire** à la place (zéro friction, déjà natif ECS, pas d'outil externe à opérationnaliser tout de suite). JFrog reste prévu mais **sans date fixée** : un chantier séparé, pas rattaché à un sprint numéroté. GHCR continue d'être utilisé côté Azure App Service en parallèle — le registre n'est pas unique multi-cloud pour l'instant, contrairement à l'intention d'origine de cette ligne. |
| Jenkins | Non mentionné | Évalué **en parallèle de GitHub Actions à partir du Sprint 9** (Kubernetes) — pas avant, tant que les cibles restent PaaS/serverless (App Service, ECS Fargate) où GH Actions suffit. Un seul pipeline porté en test, sans rien couper côté GH Actions. |
| Puppet / Chef | Non mentionné | **Écartés du roadmap.** Aucune VM longue durée à maintenir dans ce plan (App Service = PaaS, ECS Fargate = serverless, EKS/AKS = nodes managés) : pas de terrain d'usage réel pour un outil de config management. |
| Kubernetes | Step 9, testable localement avant AKS/EKS | Confirmé en **Sprint 9**, après les fondations Azure (Sprint 4) et AWS (Sprint 5). `deploy/kubernetes/` reste un dossier vide (placeholder) jusque-là. |
| Outillage qualité de code / architecture / dette technique / gouvernance d'entreprise | Non spécifié | **Distribué dans les Sprints 6 à 10 existants** (pas de sprint dédié) — chaque outil rejoint le sprint où il a du contenu réel à analyser plutôt que d'attendre un sprint isolé en fin de roadmap. Stack majoritairement gratuite/open-source ; **NDepend** est le seul outil payant retenu (essai 14 jours puis licence ~500-900$/an), pour l'analyse de dépendances/cycles/SOLID spécifique .NET qu'aucun outil gratuit n'égale vraiment. **LeanIX et Sparx Enterprise Architect écartés** : outils de cartographie de portefeuille multi-applications (dizaines d'apps, plusieurs business units) — hors d'échelle pour un produit unique, même à maturité Sprint 10. Détail complet : Step 18. |
| Adoption TOGAF 10.0 | Non spécifié | **Nouveau Sprint 11, dédié, en fin de roadmap** (pas d'interruption du Sprint 5 en cours ni des Sprints 6-10 déjà planifiés) — rigueur complète demandée : le cycle ADM est suivi dans son intégralité (Preliminary + Phases A à H + Requirements Management), avec tous les livrables formels de chaque phase, pas une version allégée. Choix explicite de démarrer ce sprint une fois la plateforme réellement construite (Azure + AWS + Angular + microservices + Kubernetes + SRE), pour que les Architectures Business/Data/Application/Technology documentent un système réel plutôt qu'anticipent un système qui n'existe pas encore. Les documents déjà produits (README, ce roadmap, `docs/architecture.md`, les états des lieux Sprint 1-4 et Sprint 4/5) seront **réécrits en terminologie/structure TOGAF** dans le cadre de ce sprint plutôt que dupliqués à côté. Détail complet : Step 19. |
| Garde-fou coût Azure (`modules/azure/cost-guard`) | Non spécifié | **Décision prise en session (août 2026)**, suite à la découverte de deux plans App Service Basic B1 non partagés tournant en continu : un budget Azure Cost Management de **7 €/mois**, scopé au resource group `rg-arkcloud-dev` (pas à l'abonnement — le storage account Terraform state ne doit pas compter). À 100 % de dépassement, un Automation Runbook (identité managée, least-privilege scopé au serveur Postgres uniquement) **arrête PostgreSQL Flexible Server automatiquement** — ce n'est **pas** une bascule App Service vers un tier moins cher : vérifié en direct que Free (F1) et Shared (D1) ne supportent pas l'intégration VNet régionale dont l'API a besoin pour atteindre Postgres en privé (`az appservice plan update --sku F1` échoue tant que l'app y est rattachée), donc Basic B1 est déjà le tier le moins cher qui reste fonctionnel côté App Service — pas de palier de repli gracieux à automatiser là. Limite connue : les données de coût Azure ne sont pas temps réel (latence de plusieurs heures documentée par Microsoft), donc le déclenchement peut arriver après que le seuil soit déjà dépassé, avec un peu de dépense supplémentaire entre-temps. Côté AWS, rien de construit pour l'instant — Cost Explorer n'est pas encore activé sur le compte, donc pas de vrais chiffres pour dimensionner un seuil équivalent. |
| Communication inter-services, fitness functions & ADR | Non spécifiés — trois absences du roadmap d'origine | **Ajoutés en session (août 2026)** après revue du plan sous l'angle « qu'attendrait un architecte senior de ce livrable ». Trois manques structurants, chacun rattaché au sprint où il a du contenu réel : (1) **le Sprint 8 découpait en microservices sans jamais décider comment ils communiquent** — ni synchrone/asynchrone, ni propriété des données, ni cohérence transactionnelle, ce qui mène mécaniquement au « monolithe distribué ». Comblé par le **Step 16 quater** : Kafka comme log d'événements (et non file de messages — la rejouabilité et les consommateurs multiples sont le critère de choix face à RabbitMQ/SQS), plus les mécanismes sans lesquels un broker ne suffit pas : transactional outbox, idempotence des consommateurs, Schema Registry, dead letter topic, cohérence à terme assumée. (2) **Aucune caractéristique d'architecture n'était nommée, priorisée ni mesurée** — l'outillage qualité couvrait le code et les dépendances, jamais la performance/disponibilité/coût comme propriétés vérifiables. Comblé par le **Step 18.7** : au maximum trois caractéristiques prioritaires explicitement retenues (et celles sacrifiées documentées), chacune adossée à une fitness function bloquante en CI plutôt qu'à un rapport qu'on lit à la main. (3) **Aucun format ADR** — les décisions vivaient dans ce journal et dans des commentaires de code, sans traçabilité par décision ni conséquences négatives assumées par écrit. `docs/adr/` devient l'entrée de l'Architecture Repository TOGAF du Sprint 11, plutôt qu'un travail parallèle à réconcilier ensuite. **Trois manques supplémentaires comblés dans la foulée** : (4) **contrats d'API et résilience des appels synchrones** (Step 16 quinquies) — versionnement explicite, tests de contrat Pact pour que casser un consommateur bloque la CI plutôt que d'apparaître en production, et timeouts/retry-backoff-jitter/circuit breaker/bulkhead via Polly, chacun devant être *prouvé* par une expérience Chaos Mesh plutôt que déclaré ; (5) **stratégie de déploiement** (Step 16 sexies) — le pipeline déployait sans jamais limiter le rayon d'impact d'une mauvaise version : blue-green au Sprint 9, canary avec rollback automatique sur métriques au Sprint 10, feature flags, et surtout migrations de base compatibles en avant (deux versions du code contre une seule base pendant un canary — le vrai point dur) ; (6) **analyse de risque structurée** (risk storming, dans le Step 18.7) — les risques de ce projet étaient tous découverts au moment où ils se manifestaient ; désormais cotés probabilité × impact à froid, avec décision explicite (mitiger/accepter/transférer/éliminer) tracée en ADR, un risque accepté et documenté étant une décision d'architecture tandis qu'un risque accepté et tu est un accident en attente. |
| Architecture cible Azure/AWS (post-Sprint 5) | Non spécifié — le roadmap ne définissait aucun état final pour la coexistence des deux clouds | **Décision prise en session (août 2026)** : Azure et AWS restent **parallèles et actifs simultanément à court terme** (état actuel — deux copies complètes et indépendantes du stack, sans lien fonctionnel : bases séparées, secrets/clés JWT séparés, aucun routage ni bascule entre elles). **Cible à terme : primaire + DR (bascule)**, pas un actif-actif symétrique — choix cohérent avec la pratique entreprise réelle (coût/complexité de l'actif-actif rarement justifié). Le patron retenu pour la migration future est le **warm standby** : un cloud primaire sert tout le trafic, le secondaire tourne à capacité réduite avec réplication de données en continu, prêt à être promu. Trois chantiers identifiés comme bloquants pour cette migration, non commencés : (1) réplication logique PostgreSQL cross-cloud primaire→secondaire (le vrai point dur — la donnée, pas le compute), (2) couche DNS/health-check agnostique au-dessus des deux clouds (ni Traffic Manager ni Route 53 seuls ne supervisent nativement l'autre cloud), (3) unification de la clé de signature JWT entre Key Vault et Secrets Manager (sinon un token émis par un cloud est invalide sur l'autre après bascule). Pas de sprint numéroté attribué pour l'instant — candidat naturel pour le travail Data Architecture du Sprint 11 (TOGAF), qui prévoit déjà un Data Migration diagram Azure↔AWS. |

---

## 🗺️ Correspondance avec les sprints

| Sprint | Contenu | Outillage qualité/architecture/gouvernance ajouté *(Step 18)* | Statut |
|---|---|---|---|
| 1 | Cadrage + backend de base | — | ✅ Fait |
| 2 | Qualité backend (validation, middleware, logging, tests, docker compose) | — | ✅ Fait |
| 3 | Auth JWT + Blazor | — | ✅ Fait |
| 4 | **CI/CD + Azure** (ce document, Steps 1–9 partie Azure) | — | ✅ Clôturé (28/07/2026) |
| 5 | AWS foundation (Steps 10–12) | — | ✅ Clôturé |
| 6 | Sécurité cloud avancée (Step 16) | SonarQube/SonarCloud, NDepend *(payant)*, ArchUnitNET, Snyk, Renovate, **ADR + caractéristiques d'architecture priorisées (Step 18.7)** | 🔄 En cours |
| 7 | Angular enterprise | Compodoc, extension SonarCloud au TypeScript/Angular | ⏳ À venir |
| 8 | Microservices **+ messagerie Kafka & patterns distribués (16 quater), contrats d'API & résilience (16 quinquies)** | Structurizr Lite + Mermaid (C4), Swagger/OpenAPI + Redocly par service, Schema Registry, Pact, Polly | ⏳ À venir |
| 9 | Kubernetes (Step 9 pour de vrai, sur cluster managé) **+ blue-green (16 sexies)** | Open Policy Agent / Gatekeeper | ⏳ À venir |
| 10 | SRE / plateforme **+ canary automatisé sur métriques & feature flags (16 sexies)** | Grafana + Prometheus, k6, Backstage, **fitness functions performance/coût bloquantes en CI (Step 18.7)** | ⏳ À venir |
| 11 | **Adoption TOGAF 10.0 & réalignement architecture d'entreprise** (Step 19) | Cycle ADM complet (Preliminary + A à H), tous livrables formels | ⏳ À venir (fin de roadmap) |

---

## Step 1 — Organisation du repository

**Structure actuelle (réelle, pas celle de la doc d'origine) :**

```
ArkCloud/
│
├── .github/workflows/
│   ├── arkcloud-backend-ci.yml
│   └── arkcloud-frontend-ci.yml
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

**Structure cible pour `ArkCloudInfra` (repo séparé) :**

```
ArkCloudInfra/
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

Dans `ArkCloudInfra/environments/<env>/`, chaque environnement contient :

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
- `snet-api` (`10.10.1.0/24`) — intégration VNet sortante pour le Plan d'`ArkCloud.API`, seul autorisé à atteindre la base.
- `snet-web` (`10.10.4.0/24`) — intégration VNet sortante pour le Plan d'`ArkCloud.Blazor`. Séparé de `snet-api` : Azure lie un subnet d'intégration VNet à un seul App Service Plan, donc API et Blazor (deux Plans distincts) ne peuvent pas partager un subnet.
- `snet-database` (`10.10.2.0/24`) — délégué à PostgreSQL Flexible Server.
- `snet-private-endpoint` (`10.10.3.0/24`) — réservé, private endpoints (Key Vault, storage) à venir en durcissement (Sprint 6).

> Correction apportée en session : la première version ne comptait qu'un seul `snet-app` partagé par API et Blazor — mélangeant deux Plans distincts sur un même subnet, ce qu'Azure interdit techniquement, et n'exprimant pas la vraie frontière de confiance (Blazor ne doit jamais parler à PostgreSQL directement).

### 7.3 Network Security Groups
- `nsg-api` : pas de règle entrante custom (l'intégration VNet est sortante uniquement, rien n'écoute d'entrant sur ce subnet) — laissé en place pour durcissement futur (Sprint 6).
- `nsg-web` : `Deny` explicite en sortant sur `5432` vers `snet-database` — défense en profondeur, rend la règle "Blazor ne parle jamais à PostgreSQL" vérifiable au niveau réseau, pas juste une convention de code.
- `nsg-database` : `Allow` entrant `5432` uniquement depuis `snet-api`.
- Tout le reste : refusé (règles implicites Azure).

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

> ✅ Fait — les Dockerfiles vivaient dans `backend/ArkCloud.API/` et `frontend/ArkCloud.Blazor/`, déplacés sous `deploy/docker/Dockerfile.api` / `Dockerfile.blazor`. `docker-compose.yml` et les deux workflows (`arkcloud-backend-ci.yml`, `arkcloud-frontend-ci.yml`) mis à jour en conséquence. Le contenu des Dockerfiles n'a pas changé — seul l'emplacement/nom du fichier, le contexte de build reste la racine du repo (`COPY . .` s'appuie dessus, pas sur l'emplacement du Dockerfile).

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

> Révisé (task #33) : utilise **Amazon ECR** (2 repos, `modules/aws/ecr`), pas JFrog — JFrog reste une migration future non datée. GHCR continue d'alimenter Azure App Service en parallèle ; les deux clouds ont donc chacun leur propre registre pour l'instant, pas un registre unique partagé. Le vrai bloquant actuel n'est pas le registre mais la CI d'ArkCloud, qui ne pousse encore que vers GHCR — tant qu'elle n'est pas mise à jour pour pousser aussi vers ces 2 repos ECR (task #38), les tâches ECS ne peuvent pas démarrer (`CannotPullContainerError`).

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

Dans `.github/workflows/` (réparti entre les deux repos, vu la séparation ArkCloud / ArkCloudInfra) :

**Côté ArkCloud :**
```
arkcloud-backend-ci.yml       (existe — restore/build/test/publish/push image)
arkcloud-frontend-ci.yml      (existe)
```
> ✅ Fait — scan Trivy (`aquasecurity/trivy-action@0.36.0`) ajouté juste après le build de l'image, avant tout push : `CRITICAL`/`HIGH` avec correctif disponible font échouer le job (`ignore-unfixed: true`, `exit-code: "1"`).

À enrichir : déclenchement cross-repo vers `ArkCloudInfra` (ex. `repository_dispatch`) pour lancer le déploiement du nouveau tag d'image.

**Côté ArkCloudInfra :**
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

> **NSG flow logs — fait (Sprint 6), mais pas des NSG flow logs au final** : `modules/azure/flow-logs` — Network Watcher (référencé via data source, celui auto-créé par Azure existe déjà depuis la création du VNet), storage account dédié, rétention 30 jours. Bug réel découvert à l'apply : Azure a retiré la création de **nouveaux** NSG flow logs le 30/06/2025 (retraite complète le 30/09/2027) — confirmé contre la doc officielle du provider AzureRM et le guide de migration Microsoft. Remplacé par les **Virtual Network Flow Logs** : même ressource Terraform (`azurerm_network_watcher_flow_log`), `target_resource_id` pointé sur le VNet entier plutôt que `network_security_group_id` sur chaque NSG — un seul flow log couvre les 4 sous-réseaux (api/web/database/private-endpoint) au lieu d'un par NSG. Traffic Analytics désactivé par défaut (`azure_enable_traffic_analytics = false`) — facturé au Go traité en plus des flow logs eux-mêmes, activable plus tard sans changement de code si un vrai besoin de requêtage apparaît.

---

## Step 16 bis — Exploiter le plein potentiel du multi-cloud *(post-Sprint 5, distribué Sprints 6/9/10)*

Suite à la décision d'architecture cible Azure/AWS (Journal des décisions) : aujourd'hui, les deux clouds sont deux copies parallèles sans lien fonctionnel. Ce qui suit sont les chantiers qui transformeraient ça en un vrai système multi-cloud plutôt qu'une coïncidence de deux déploiements similaires — chacun rattaché au sprint où il a du sens, pas un sprint dédié isolé.

**Sprint 6 (Sécurité cloud avancée) :**
- **Réseau privé cross-cloud** — VPN site-to-site ou interconnect dédié entre le VPC AWS et le VNet Azure, pour que la future réplication PostgreSQL primaire→secondaire (bloquant identifié pour le DR) ne transite jamais par l'internet public.
- **Identité fédérée unique** — Azure AD/Entra ID comme IdP fédéré vers AWS IAM (SAML/OIDC), pour arrêter de gérer deux systèmes d'identité/rôles indépendants.
- **Unification de la clé de signature JWT** — un seul secret source de vérité, synchronisé entre Key Vault et Secrets Manager, pour qu'un token émis par un cloud reste valide après une bascule vers l'autre.

**Sprint 9 (Kubernetes) :**
- **Portabilité réelle** — un seul jeu de manifests Kubernetes (Step 9) déployé indifféremment sur AKS ou EKS sans divergence, comme vrai test de portabilité multi-cloud plutôt que deux Terraform/Dockerfiles qui se ressemblent par coïncidence.
- **Policy-as-code commune** — les politiques Open Policy Agent/Gatekeeper (déjà prévues Step 18.5) appliquées identiquement aux deux clusters, pour que "sécurisé sur Azure" et "sécurisé sur AWS" soient la même règle et non deux checklists qui divergent avec le temps.

**Sprint 10 (SRE / plateforme) :**
- **Observabilité unifiée** — Grafana (déjà prévu Step 18.6) comme pane unique agrégeant CloudWatch et Application Insights/Log Analytics, pour ne plus avoir deux dashboards séparés à surveiller en cas d'incident.
- **FinOps cross-cloud** — visibilité de coût agrégée (AWS Cost Explorer + Azure Cost Management) dans un seul rapport, plutôt que deux factures suivies séparément.

**Explicitement écarté, hors d'échelle pour ce projet :** arbitrage de coût dynamique entre clouds (déplacer la charge vers le moins cher en temps réel) et présence géographique multi-provider (latence utilisateur par région) — leviers réels seulement à un volume et une base utilisateur qu'un projet solo n'atteint pas. À éviter aussi : sur-abstraire le code pour rester au plus petit dénominateur commun des deux clouds — la vraie portabilité viendra de Kubernetes (ci-dessus), pas de l'évitement des services managés natifs (RDS/Secrets Manager/ALB côté AWS, App Service/Key Vault côté Azure), qui feraient perdre l'intérêt de ces services sans gain mesurable.

> Rattachement Sprint 11 (TOGAF) : la réplication de données Postgres cross-cloud mentionnée ci-dessus alimente directement le Data Migration diagram déjà prévu en Phase C (Data Architecture).

---

## Step 16 ter — Tests de charge & chaos engineering *(Sprints 9/10, pas de volet Sprint 6)*

Objectif : simuler du vrai flux et pousser l'app en conditions extrêmes pour valider expérimentalement ce qui, jusqu'ici, n'est que construit — capacité réelle, comportement en panne, et fiabilité du monitoring (Step 15) censé tout voir.

> **Révisé en session** : la version initiale prévoyait AWS Fault Injection Simulator (FIS) et Azure Chaos Studio dès le Sprint 6, avant Kubernetes. Retiré du plan — ces deux outils sont explicitement remplacés par Chaos Mesh dès que Kubernetes existe (Sprint 9), donc les construire en Sprint 6 aurait été un travail jetable avec une durée de vie de 2-3 sprints. Sauter directement à Chaos Mesh évite ce gâchis. k6 reste indépendant de Kubernetes (il tape juste sur l'ALB/App Service existants) mais n'a pas de raison d'être scindé en avance — regroupé entièrement en Sprint 10, là où Grafana/Prometheus/k6 sont déjà prévus ensemble (Step 18.6), plutôt que dispersé sur trois sprints.

**Sprint 9 (Kubernetes) :**
- **Chaos Mesh** — dès AKS/EKS en place, outil unique Kubernetes-natif et cloud-agnostique pour les expériences de chaos, unifié entre les deux clusters plutôt que deux outils propriétaires séparés.

**Sprint 10 (SRE / plateforme) :**
- **k6 — tous les profils** (smoke, load, stress, spike, soak) sur les endpoints critiques, en environnement `dev` uniquement — le soak (charge modérée soutenue sur plusieurs heures) est ce qui révèle les fuites (connexions DB non fermées, mémoire qui grossit) qu'un test court ne montre jamais.
- **Corrélation test ↔ monitoring** — chaque test k6/Chaos Mesh est observé via CloudWatch/Application Insights et le futur Grafana unifié (Step 16 bis) : un test qui ne fait pas sonner l'alarme censée se déclencher est un bug de monitoring, pas un test réussi.
- **Drill de bascule réel** — une fois le primaire+DR (Journal des décisions) en place, déclencher une vraie panne du primaire *sous charge k6* via Chaos Mesh, pour mesurer le RTO réel plutôt qu'un RTO théorique sur papier.

> Prudence coût : ces tests génèrent des coûts réels (transfert NAT Gateway, IOPS RDS, requêtes). Cantonnés strictement à l'environnement `dev`, avec alerte de budget avant un stress test agressif et scale-down explicite juste après.

---

## Step 16 quater — Communication inter-services & messagerie *(Sprint 8, prérequis du découpage microservices)*

> **Trou identifié en session (août 2026)** : le Sprint 8 prévoyait de découper le monolithe en microservices sans jamais décider **comment ces services communiquent**. C'est le manque le plus structurant du roadmap tel qu'il était — découper sans trancher synchrone/asynchrone, ni la propriété des données, ni la cohérence transactionnelle, produit un « monolithe distribué » : tous les inconvénients du distribué, aucun de ses bénéfices. Cette section comble ce trou.

### Le choix de style, avant l'outil

La question n'est pas « Kafka ou pas Kafka » mais **quelle interaction pour quel besoin** :

| Besoin | Style | Justification |
|---|---|---|
| Lecture immédiate nécessaire à la réponse (ex. l'API commandes doit valider qu'un produit existe) | **Synchrone (REST/gRPC)** | Le résultat est requis pour répondre à l'utilisateur ; passer par un broker n'apporterait qu'une latence et une complexité inutiles. |
| Notification d'un fait accompli (ex. « commande payée ») que d'autres services doivent traiter sans bloquer l'appelant | **Asynchrone (événements)** | C'est le cas d'usage réel de Kafka ici — le service commandes n'a pas à savoir qui écoute ni à attendre. |
| Workflow multi-services avec besoin de compensation (ex. commande → paiement → stock → expédition) | **Saga (orchestration ou chorégraphie)** | Il n'y a pas de transaction ACID distribuée possible entre bases séparées : soit un orchestrateur explicite, soit une chaîne d'événements avec compensations. À trancher explicitement, pas à subir. |

### Kafka — pourquoi, et pourquoi Sprint 8 et pas avant

**Apache Kafka** (managé : Azure Event Hubs avec l'API Kafka / Amazon MSK, ou self-hosted sur Kubernetes au Sprint 9) comme **log d'événements durable**, pas comme simple file de messages. Ce que ça apporte concrètement ici :

- **Découplage temporel** — un consommateur arrêté ne fait pas perdre d'événements, il rattrape à son rythme.
- **Rejouabilité** — le log est conservé, donc un nouveau service peut se brancher et rejouer l'historique ; une file classique (RabbitMQ/SQS) supprime le message après consommation.
- **Plusieurs consommateurs indépendants** du même flux, chacun avec son propre offset.

Pas avant le Sprint 8 : avec un seul service applicatif, un broker n'a rien à transporter — ce serait de l'infrastructure sans usage, exactement le travers évité pour Backstage (Sprint 10) et Structurizr (Sprint 8).

**Alternative écartée** : RabbitMQ / Azure Service Bus / SQS — parfaits pour du travail en file (une tâche, un consommateur, puis suppression), mais sans rejouabilité ni consommation multiple du même flux. Le besoin ici (plusieurs services réagissant au même fait métier, historique conservé) est un log d'événements, pas une file.

### Ce que Kafka seul ne règle pas — les mécanismes à construire avec

Un broker ne rend pas un système distribué correct. Les patterns ci-dessous sont ce qui sépare « on a mis Kafka » d'une architecture événementielle réellement fiable :

- **Transactional outbox** — écrire en base *et* publier un événement ne peut pas être atomique entre deux systèmes. L'événement est écrit dans une table `outbox` **dans la même transaction** que le changement métier, puis publié par un relais séparé. Sans ça : commandes enregistrées dont l'événement n'est jamais parti, ou l'inverse.
- **Idempotence des consommateurs** — Kafka garantit *at-least-once*, donc un message *sera* rejoué un jour. Chaque consommateur doit produire le même résultat s'il traite deux fois le même événement (clé d'idempotence persistée, pas juste « on espère »).
- **Schémas versionnés des événements** (Schema Registry, Avro/JSON Schema) — un événement est un **contrat public** entre services, plus dur à changer qu'une API REST car les consommateurs sont invisibles de l'émetteur. Sans registre, un champ renommé casse silencieusement un service qu'on avait oublié.
- **Dead letter topic + politique de retry** — que se passe-t-il pour un message qu'un consommateur n'arrive jamais à traiter ? Sans DLQ explicite, soit il bloque la partition indéfiniment, soit il est perdu.
- **Cohérence à terme assumée** — les lectures cross-services renvoient des données potentiellement en retard. C'est une propriété à documenter et à rendre acceptable métier, pas un bug à corriger.

### Sprint 8 — décisions à formaliser (chacune en ADR, voir Step 18.7)

- [ ] Granularité des services — quels services, et **pourquoi** ceux-là (facteurs de découpage vs facteurs de regroupement, explicités)
- [ ] Propriété des données — un service = ses tables, aucun accès direct à la base d'un autre
- [ ] Communication synchrone vs asynchrone, cas par cas, selon la grille ci-dessus
- [ ] Saga : orchestration ou chorégraphie (et pourquoi) pour le cycle de vie d'une commande
- [ ] Kafka managé vs self-hosted sur Kubernetes (Sprint 9)
- [ ] Outbox, idempotence, Schema Registry, DLQ — construits **avec** le premier producteur, pas ajoutés après coup
- [ ] Résilience des appels synchrones restants : timeouts, retry avec backoff, circuit breaker (Polly côté .NET)

---

## Step 16 quinquies — Contrats d'API & résilience des appels synchrones *(Sprint 8)*

> Complète le Step 16 quater : celui-là traite de ce qui transite entre services, celui-ci de ce qui se passe quand le service d'en face change ou ne répond pas.

### Versionnement d'API et tests de contrat

Dès qu'un service a plus d'un consommateur, son API devient un contrat qu'on ne peut plus changer unilatéralement. Le risque réel n'est pas de casser un consommateur connu — c'est de casser celui qu'on avait oublié.

- [ ] **Stratégie de versionnement explicite** — versionnement dans l'URL (`/v1/orders`) retenu par défaut : le plus lisible dans les logs, les traces et les métriques, au prix d'URLs moins « pures » que la négociation par header. À trancher en ADR, pas par défaut implicite.
- [ ] **Règle de compatibilité** — ajouts uniquement (nouveau champ optionnel) sur une version existante ; tout retrait ou renommage impose une nouvelle version. Vaut aussi bien pour les APIs REST que pour les **schémas d'événements Kafka** (Step 16 quater), qui sont plus dangereux encore : l'émetteur ne sait pas qui consomme.
- [ ] **Tests de contrat (Pact)** — le consommateur publie ses attentes, le producteur les vérifie en CI. Le producteur ne peut pas merger un changement qui casse un consommateur, même sans jamais lire son code. C'est ce qui remplace « on espère que personne n'utilisait ce champ ».
- [ ] **Politique de dépréciation** — une version obsolète est annoncée, instrumentée (métrique d'usage résiduel par version), puis retirée **quand la métrique tombe à zéro** — pas à une date arbitraire.

### Résilience des appels synchrones restants

Tous les appels ne deviennent pas asynchrones (voir la grille du Step 16 quater). Ceux qui restent synchrones traversent le réseau : ils échoueront, et le défaut d'un `HttpClient` .NET (attente quasi infinie, aucun retry) est le pire comportement possible en distribué.

- [ ] **Timeouts explicites partout** — un appel sans timeout transforme la lenteur d'un service en panne de tous ses appelants. À définir par appel selon le budget de latence de la requête entrante, pas une valeur globale.
- [ ] **Retry avec backoff exponentiel + jitter** — uniquement sur les erreurs *transitoires* (5xx, timeout réseau), jamais sur une 4xx. Le jitter est indispensable : sans lui, tous les clients réessaient en même temps et achèvent le service qui se relevait.
- [ ] **Circuit breaker** — après N échecs consécutifs, arrêter d'appeler pendant un temps donné et échouer immédiatement. Évite qu'un service en panne consomme les threads/connexions de tous ses appelants jusqu'à les faire tomber aussi (défaillance en cascade).
- [ ] **Bulkhead** — cloisonner les pools de connexions par dépendance : un service lent ne doit pas pouvoir épuiser les ressources partagées et entraîner les autres.
- [ ] **Dégradation gracieuse** — pour chaque dépendance, décider explicitement ce que fait l'appelant quand elle est indisponible : valeur par défaut, cache périmé servi, ou échec propre. Ne jamais laisser ce comportement émerger par accident.
- [ ] Implémentation .NET : **Polly** (via `Microsoft.Extensions.Http.Resilience`), branché sur les `HttpClient` typés déjà en place (`ArkCloud.Blazor/Program.cs` en a cinq).

> **Validation, pas déclaration** : chacun de ces mécanismes doit être *prouvé* par une expérience Chaos Mesh au Sprint 9 (Step 16 ter) — un circuit breaker jamais vu s'ouvrir en conditions réelles est une hypothèse, pas une protection.

---

## Step 16 sexies — Stratégie de déploiement & rayon d'impact *(Sprint 9-10)*

> **Quatrième trou** : le roadmap construit un pipeline qui déploie, mais ne dit nulle part **comment limiter les dégâts d'une mauvaise version** ni revenir en arrière vite. Aujourd'hui, un `terraform apply` ou un push d'image remplace la version en production d'un coup, pour 100 % du trafic — c'est acceptable en dev, pas au-delà.

- [ ] **Séparer déploiement et activation** — déployer du code n'est pas le mettre en service. C'est ce qui rend tout le reste possible.
- [ ] **Blue-green** (Sprint 9, natif sur Kubernetes) — deux environnements identiques, bascule du trafic en une opération, retour arrière tout aussi rapide. Simple, mais double le coût pendant la bascule.
- [ ] **Canary / déploiement progressif** (Sprint 10) — 1 %, puis 10 %, puis 100 % du trafic, avec **arrêt automatique sur métriques** : si le taux d'erreur ou la latence de la nouvelle version dépasse le seuil, rollback sans intervention humaine. C'est la version utile du canary ; sans automatisation sur métriques, ce n'est qu'un déploiement lent.
- [ ] **Feature flags** — découpler la livraison de code de l'activation de fonctionnalité. Permet de désactiver une fonctionnalité problématique sans redéployer, et de tester en production sur un sous-ensemble d'utilisateurs. Attention à la dette : chaque flag a une date de retrait, sinon ils s'accumulent en branches mortes.
- [ ] **Migrations de base compatibles en avant** — le point dur réel : pendant un canary, deux versions du code tournent contre **une seule** base. Toute migration doit être compatible avec l'ancienne version (ajout de colonne nullable, jamais de renommage direct — pattern expand/contract en deux déploiements).
- [ ] **Critère de rollback documenté** — quelle métrique, quel seuil, quel délai déclenchent un retour arrière, décidé **avant** l'incident et non pendant.

---

## Step 18.7 — Fitness functions & caractéristiques d'architecture *(Sprint 6 → 10, continu)*

> **Deuxième trou identifié en session** : le roadmap outillait la qualité de code (SonarQube), les dépendances (Snyk) et l'architecture logicielle statique (NDepend/ArchUnitNET) — mais rien ne vérifiait automatiquement que les **caractéristiques d'architecture** (performance, disponibilité, coût, sécurité) restent dans leurs limites au fil des livraisons. C'est précisément ce que sont les fitness functions.

### D'abord : nommer et prioriser les caractéristiques

Une fitness function n'a de sens que si elle mesure une caractéristique explicitement retenue. Aucune architecture ne peut tout maximiser — le premier livrable est donc de **choisir**, et d'assumer ce qui n'est pas prioritaire :

- [ ] Lister les caractéristiques candidates du système (performance, disponibilité, scalabilité, sécurité, testabilité, déployabilité, évolutivité, observabilité, coût d'exploitation…)
- [ ] **En retenir au maximum trois comme prioritaires**, avec la justification métier ; documenter explicitement celles délibérément sacrifiées
- [ ] Rendre chacune **mesurable** — « l'app doit être rapide » n'est pas une caractéristique, « p95 de `/api/orders` < 500 ms sous 50 utilisateurs concurrents » en est une

### Ensuite : une fitness function par caractéristique retenue

Une fitness function est un **test automatisé de la caractéristique**, exécuté en CI, qui casse le build quand l'architecture dérive — exactement comme un test unitaire casse le build quand le comportement dérive.

| Type | Déjà en place | À construire |
|---|---|---|
| **Structurelle** | ArchUnitNET (règles de dépendance entre couches, Sprint 6) | Étendre aux frontières de services au Sprint 8 : un service ne référence jamais le namespace interne d'un autre |
| **Sécurité** | Checkov, Trivy, Snyk (bloquants en CI) | Fitness function « aucun secret en clair » et « TLS partout » vérifiée sur l'infra déployée, pas seulement sur le code |
| **Performance** | — | Seuil k6 en CI (Sprint 10) : la PR échoue si p95 dépasse le seuil défini, plutôt qu'un test de charge dont on regarde le résultat à la main |
| **Disponibilité / résilience** | — | Chaos Mesh (Sprint 9) exécuté en pipeline avec assertion : le système reste dans son SLO pendant l'expérience, sinon échec |
| **Coût** | Budget Azure 7 €/mois (garde-fou runtime, Sprint 6) | Fitness function CI : `infracost` sur le plan Terraform, PR bloquée si le delta dépasse un seuil — détecte la dérive **avant** l'apply, pas après la facture |
| **Observabilité** | Alarmes CloudWatch / Application Insights | Vérification automatisée qu'une alarme se déclenche réellement pendant un test de charge (déjà prévu Step 16 ter, à formaliser en fitness function) |

### Architecture Decision Records (ADR) — le support de tout le reste

> **Troisième trou** : les décisions d'architecture de ce projet vivent aujourd'hui dans le « Journal des décisions » de ce document et dans des commentaires de code (souvent excellents, mais dispersés). Aucun format ADR standard, aucune traçabilité par décision.

- [ ] Adopter un format ADR minimal dans `docs/adr/` — un fichier par décision : contexte, options envisagées, décision, **conséquences assumées** (positives *et* négatives)
- [ ] Rétro-documenter les décisions structurantes déjà prises (repo séparé pour Terraform, ECR provisoire vs JFrog, multi-cloud parallèle puis primaire+DR, certificat auto-signé sur l'ALB, rotation automatique Postgres mais pas JWT…)
- [ ] Toute décision du Sprint 8 (granularité, saga, Kafka) documentée en ADR **au moment de la décision**, pas reconstituée après
- [ ] Les ADR deviennent l'entrée de l'Architecture Repository TOGAF au Sprint 11 (Step 19) plutôt qu'un travail parallèle

### Analyse de risque structurée (risk storming) — Sprint 6, puis à chaque changement structurant

> **Cinquième trou** : les risques de ce projet sont réels et connus au coup par coup (mots de passe exposés dans le chat, certificat auto-signé, deux dossiers Terraform divergents, PAT qui expire…), mais toujours découverts **au moment où ils se manifestent**. Aucune démarche ne les identifie à froid, avant l'incident.

Le principe : évaluer le risque **par zone du système**, de façon délibérée et répétable, plutôt que d'attendre qu'un incident le révèle. Sur un projet solo, l'exercice collectif se réduit à une revue disciplinée — mais la matrice et la traçabilité restent.

- [ ] **Identifier les zones de risque** par caractéristique d'architecture retenue (Step 18.7) : disponibilité, sécurité, perte de données, performance, dette/évolutivité, coût
- [ ] **Coter chaque risque** sur deux axes — probabilité × impact — en produisant une note de criticité, plutôt qu'un ressenti « ça craint un peu »
- [ ] **Décider explicitement** pour chaque risque critique : mitiger, accepter (avec justification écrite), transférer, ou éliminer. Un risque accepté et documenté est une décision d'architecture ; un risque accepté et tu est un accident en attente.
- [ ] **Rattacher à un ADR** chaque mitigation retenue, et à une fitness function chaque risque qui peut être surveillé automatiquement
- [ ] **Refaire l'exercice** à chaque changement structurant — notamment au Sprint 8 (le passage au distribué crée une famille entière de risques nouveaux : partitions réseau, cohérence à terme, défaillance en cascade) et au Sprint 9 (Kubernetes)

**Risques déjà identifiables aujourd'hui, à formaliser lors du premier exercice** : source unique de défaillance sur la base (Postgres Burstable mono-instance, pas de HA), certificat auto-signé sur l'ALB AWS, absence de réplication cross-cloud alors que le DR est une cible déclarée, `Jwt:Key` non rotatable sans déconnecter tous les utilisateurs, dépendance à un PAT GitHub personnel dans le chemin de déploiement.

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
- [x] Registre AWS (ECR, provisoire — task #33)
- [ ] Registre JFrog Artifactory *(migration future, pas de date fixée)*
- [ ] Évaluation Jenkins *(Sprint 9)*

**Sécurité**
- [ ] IAM/RBAC least privilege
- [ ] Chiffrement au repos (KMS / Key Vault)
- [x] TLS partout — Azure App Service a HTTPS par défaut (certificat Microsoft) ; **AWS ALB en HTTPS depuis Sprint 6** avec un certificat auto-signé (pas de domaine réel disponible pour un certificat ACM validé par DNS) — navigateur affiche un avertissement de confiance, mais le trafic est chiffré ; HTTP redirige désormais vers HTTPS au lieu de servir en clair. À remplacer par un certificat DNS-validé dès qu'un domaine existe (voir README §9 pour le détail complet)
- [x] Rotation des secrets — **mots de passe Postgres : rotation automatique tous les 90 jours sur les deux clouds** (Sprint 6, `modules/azure/secret-rotation` via Automation Runbook + schedule, `modules/aws/secret-rotation` via Secrets Manager + Lambda custom). `Jwt:Key` et `GHCR_PAT` restent manuels par décision explicite (rotation JWT = déconnexion de tous les utilisateurs sans support multi-clés ; PAT GitHub = aucune API cloud pour l'automatiser), couverts par les rappels d'échéance automatisés (`.github/workflows/secret-expiry-check.yml` + `.github/secrets-inventory.json`). Détail complet README §10
- [x] Audit logging activé
- [x] NSG flow logs (Sprint 6, `modules/azure/flow-logs`)

**Coûts**
- [x] Garde-fou budget Azure — 7 €/mois sur `rg-arkcloud-dev`, arrêt automatique de PostgreSQL au dépassement (`modules/azure/cost-guard`)
- [ ] Équivalent AWS — bloqué sur l'activation de Cost Explorer (accès refusé, `AccessDeniedException`)

**Qualité, architecture & gouvernance** *(Step 18, Sprints 6-10)*
- [ ] Quality gate SonarQube/SonarCloud bloquant en CI (bugs, vulnerabilities, code smells, couverture)
- [ ] NDepend — 0 violation de règle de dépendance critique (ex. Domain ne référence jamais Infrastructure)
- [ ] ArchUnitNET — tests d'architecture dans la suite de tests, exécutés en CI
- [ ] Snyk — 0 vulnérabilité critique/haute non corrigée (dépendances, images Docker, IaC)
- [ ] Renovate configuré (PRs automatiques de mise à jour de dépendances)
- [ ] Documentation d'architecture C4 à jour (Structurizr/Mermaid)
- [ ] OpenAPI/Swagger publié et versionné par service (Redocly pour la doc consommable)
- [ ] Politiques Open Policy Agent actives sur le cluster Kubernetes
- [ ] Backstage — catalogue des services/APIs/ownership à jour
- [ ] Grafana + Prometheus — dashboards de plateforme, alerting configuré
- [ ] k6 — scénarios de tests de charge sur les endpoints critiques

**Multi-cloud avancé** *(Step 16 bis, Sprints 6/9/10)*
- [ ] VPN/interconnect privé entre VPC AWS et VNet Azure
- [ ] Réplication PostgreSQL cross-cloud (primaire → secondaire)
- [ ] Identité fédérée Entra ID → AWS IAM (SAML/OIDC)
- [ ] Clé de signature JWT unifiée (secret unique, synchronisé Key Vault ↔ Secrets Manager)
- [ ] Manifests Kubernetes identiques déployés sur AKS et EKS sans divergence
- [ ] Politiques OPA/Gatekeeper communes aux deux clusters
- [ ] Grafana en pane unique agrégeant CloudWatch + Application Insights/Log Analytics
- [ ] Rapport de coût agrégé AWS Cost Explorer + Azure Cost Management

**Tests de charge & chaos engineering** *(Step 16 ter, Sprints 9/10 — FIS/Chaos Studio écartés du plan, voir note de révision Step 16 ter)*
- [ ] Chaos Mesh — unifié AKS/EKS (Sprint 9)
- [ ] k6 — tous les profils : smoke/load/stress/spike/soak sur les endpoints critiques (dev uniquement, Sprint 10)
- [ ] Alarmes de monitoring validées comme se déclenchant réellement pendant les tests
- [ ] Drill de bascule primaire→DR sous charge réelle, RTO mesuré

---

## Step 18 — Outillage qualité de code, architecture & gouvernance d'entreprise

> Objectif : couvrir les dimensions qu'un pipeline CI/CD (fmt/tflint/Checkov/Trivy, déjà en place) ne couvre pas — qualité de code dans la durée, dette technique, architecture logicielle, dépendances, documentation vivante, performance, gouvernance. Distribué dans les Sprints 6 à 10 (voir tableau plus haut) plutôt qu'un sprint dédié : chaque outil rejoint le sprint où il a du contenu réel à analyser, pas avant.
>
> **Contrainte budget retenue** : stack très majoritairement gratuite/open-source. Un seul outil payant, **NDepend**, retenu là où aucun équivalent gratuit ne couvre la même profondeur pour du .NET. LeanIX, Sparx Enterprise Architect, GitHub Advanced Security (au-delà des repos publics) et Checkmarx sont écartés — soit trop chers pour la valeur ajoutée à cette échelle, soit redondants avec un outil déjà retenu.

### 18.1 Qualité de code — Sprint 6

- **SonarQube (Community Edition, self-hosted) ou SonarCloud (gratuit en repo public)** : bugs, vulnerabilities, code smells, duplications, couverture de tests, complexité cyclomatique, security hotspots. Couvre C#/.NET, TypeScript/Angular et Blazor. Quality gate branché sur `terraform-ci.yml`/`arkcloud-backend-ci.yml`/`arkcloud-frontend-ci.yml` : PR bloquée si le gate échoue.
- Alternative écartée : **JetBrains Qodana** — solide sur .NET mais SonarQube couvre déjà C# + TypeScript dans un seul outil, pas besoin de deux plateformes de qualité de code.

### 18.2 Architecture logicielle — Sprint 6

- **NDepend** *(payant, seul outil commercial retenu)* : dépendances entre `ArkCloud.Domain`/`Application`/`Infrastructure`/`API`, détection de cycles, respect du sens de dépendance de la Clean Architecture, complexité, règles personnalisées. C'est l'outil qui aurait détecté une violation du type "Infrastructure référence Domain dans le mauvais sens" avant que ça arrive en revue de code.
- **ArchUnitNET** *(gratuit)* : les mêmes règles de couche, mais **as code**, exécutées comme tests dans `backend/tests/` — le build échoue si une règle est cassée, pas besoin d'ouvrir un rapport séparé. Complémentaire à NDepend, pas un doublon : NDepend pour l'exploration/diagnostic, ArchUnitNET pour l'enforcement continu en CI.

### 18.3 Dépendances & sécurité — Sprint 6

- **Snyk** *(free tier)* : dépendances NuGet/npm, images Docker, IaC (Terraform) — vient en complément de Trivy (déjà en place côté scan d'image) et Checkov (déjà en place côté Terraform), pas en remplacement.
- **Renovate** *(gratuit)* : mises à jour de dépendances automatisées, plus configurable que Dependabot (groupement de PRs, schedules, auto-merge sur les patchs mineurs).

### 18.4 Documentation d'architecture — Sprint 7-8

- **Mermaid** *(gratuit, déjà utilisable directement dans le Markdown GitHub)* : diagrammes légers au fil de l'eau dans `docs/`.
- **Structurizr Lite** *(gratuit, self-hosted)* : modèle C4 complet (Enterprise → Système → Conteneurs → Composants) à partir du Sprint 8 (Microservices), quand il y a plusieurs services à cartographier — avant ça, un seul système avec 4 couches ne justifie pas le modèle C4.
- **Compodoc** *(gratuit)* : documentation générée du code Angular, Sprint 7.
- **Swagger/OpenAPI** *(déjà en place côté ArkCloud.API)* **+ Redocly** *(gratuit)* : documentation API consommable, un jeu de specs par service à partir du Sprint 8.

### 18.5 Gouvernance — Sprint 9

- **Open Policy Agent (Gatekeeper)** *(gratuit)* : policies as code sur le cluster Kubernetes (Sprint 9) — admission control, refuse un manifest qui ne respecte pas les règles (ex. pas de conteneur root, limites de ressources obligatoires). Choisi à ce sprint précisément parce que c'est là qu'apparaît la ressource qu'OPA gouverne (avant Kubernetes, rien à admettre).
- **OpenRewrite** *(gratuit, hors sprint dédié)* : gardé en réserve pour les montées de version majeures (.NET, Angular) plutôt que rattaché à un sprint — outil ponctuel, pas un contrôle continu.

### 18.6 Observabilité, performance & portail développeur — Sprint 10

- **Grafana + Prometheus** *(gratuits, self-hosted ou managés)* : consolidation des métriques Azure/AWS/Kubernetes dans un seul plan d'observabilité, complète Application Insights/CloudWatch (déjà en place par cloud) sans les remplacer.
- **k6** *(gratuit)* : tests de charge sur les endpoints critiques (`orders`, `auth`) avant mise en prod.
- **Backstage** *(gratuit, open-source, self-hosted)* : portail développeur — catalogue des services, APIs, pipelines, ownership. Placé en Sprint 10 plutôt que plus tôt : avant Microservices (Sprint 8) et Kubernetes (Sprint 9), il n'y a qu'un système à cataloguer, pas assez de contenu pour justifier l'outil.

### Écartés du roadmap

| Outil | Raison |
|---|---|
| LeanIX | Cartographie de portefeuille multi-applications (business capabilities, dizaines d'apps) — hors d'échelle pour un produit unique. |
| Sparx Enterprise Architect | Même famille que LeanIX (TOGAF/ArchiMate à l'échelle d'un portefeuille) — pas de terrain d'usage ici. |
| GitHub Advanced Security | Gratuit uniquement sur repos publics ; redondant avec Snyk + Trivy + Checkov déjà en place pour un repo privé. |
| Checkmarx | Redondant avec Snyk (même catégorie SAST/dépendances), pas de valeur ajoutée à payer les deux. |

---

## Step 19 — Sprint 11 : Adoption TOGAF 10.0 & réalignement architecture d'entreprise

> Décision : rigueur complète, pas une version allégée — le cycle ADM (Architecture Development Method) est suivi dans son intégralité, avec tous les livrables formels de chaque phase, sur le même produit ArkCloud. Placé en fin de roadmap (après le Sprint 10) plutôt qu'immédiatement après le Sprint 5 : documenter Business/Data/Application/Technology Architecture n'a de sens que sur une plateforme réellement construite (Azure + AWS + Angular enterprise + microservices + Kubernetes + SRE), pas sur un système encore à moitié bâti. N'interrompt ni la fin du Sprint 5 (Steps 11/15/CI-CD cross-cloud restants) ni les Sprints 6-10 déjà planifiés.
>
> Les documents déjà produits dans ce projet (`ArkCloud/README.md`, ce roadmap, `docs/architecture.md`, les états des lieux Sprint 1-4 et Sprint 4/5, `ArkCloudInfra/README.md`) sont **réécrits en terminologie et structure TOGAF** au fil de ce sprint plutôt que dupliqués à côté — le contenu factuel qu'ils décrivent ne change pas, seule son organisation dans le Content Metamodel TOGAF change.

### Cadre : le cycle ADM et son centre

Le TOGAF 10.0 restructure le contenu autour d'un **Fundamental Content** (ADM, techniques, Content Framework, gouvernance) et de **Series Guides** optionnels. Le cœur reste le cycle ADM ci-dessous, avec la **Gestion des Exigences (Requirements Management)** au centre — alimentée par, et alimentant, chacune des phases :

```
                     Requirements Management
                    (centre du cycle, continu)
                              ↑↓
Preliminary → A: Vision → B: Business → C: Data/Application → D: Technology
                                                                      ↓
        H: Change Mgmt ← G: Implementation Gov. ← F: Migration Plan ← E: Opportunities & Solutions
```

### Préliminaire — Établir la capacité d'architecture

**Livrables :**
- **Organizational Model for Enterprise Architecture** — pour ArkCloud : rôles (qui est Architecte d'Entreprise, qui gouverne les décisions techniques), même à effectif réduit.
- **Tailored Architecture Framework** — ce document explique comment TOGAF est adapté à ArkCloud (produit unique, pas de portfolio multi-applications) plutôt que retenu tel quel.
- **Architecture Principles** (catalogue) — dérivés des décisions déjà prises et documentées dans ce roadmap et le Journal des décisions (ex. "Terraform multi-cloud dans un seul state", "pas de secret via Terraform", "audit logging vérifié par requête réelle, pas par un statut vert") reformulés comme des principes formels (énoncé, justification, implications).
- **Architecture Repository** (initialisation) — devient le point d'entrée : `docs/architecture.md`, `docs/infra-roadmap.md`, les modules Terraform eux-mêmes comme Architecture/Solution Building Blocks.

### Phase A — Architecture Vision

**Livrables :**
- **Statement of Architecture Work** (approuvé) — périmètre, objectifs, contraintes de ce cycle ADM.
- **Stakeholder Map** — même pour un produit à petite équipe : utilisateurs finaux (clients de l'API/Blazor), exploitant (toi), futurs contributeurs.
- **Business Scenarios** — les cas d'usage métier réels déjà couverts (customers/products/orders) formalisés en scénario TOGAF (acteur, déclencheur, résultat souhaité).
- **Capability Assessment** — état actuel (Sprints 1-10 réalisés) vs capacité cible.
- **Architecture Vision** (document, avec Value Chain diagram et Solution Concept diagram) — synthèse narrative, dans l'esprit des documents storytelling déjà produits pour les Sprints 4/5, mais structurée selon le gabarit TOGAF.
- **Draft Architecture Definition Document** — première version du document qui sera complété phase après phase.
- **Communications Plan** — comment les décisions d'architecture sont communiquées (ce roadmap en est déjà un exemple vivant).

### Phase B — Business Architecture

**Livrables :**
- **Business Footprint diagram** — objectifs métier → fonctions → services rendus par ArkCloud.
- **Business Service/Function Catalog** — gestion catalogue produits, gestion commandes, authentification, etc.
- **Business Interaction diagram** — comment les rôles (Admin/Manager/User, cf. `seed_users.sql`) interagissent avec ces fonctions.
- **Business Process Catalog** — cycle de vie d'une commande (Draft → Submitted → Paid/Cancelled) formalisé en tant que processus métier, pas seulement en tant que statuts d'entité.
- **Business Architecture Report** — synthèse de phase.

### Phase C — Information Systems Architecture (Data + Application)

**Data Architecture — livrables :**
- **Data Entity/Data Component Catalog** — Customer, Product, Order, OrderItem, User, Role (déjà modélisés côté EF Core), catalogués formellement.
- **Data Dissemination diagram** — où vivent ces données (PostgreSQL Azure ET AWS RDS — un vrai sujet TOGAF vu le multi-cloud).
- **Data Security diagram** — chiffrement au repos (Azure Storage encryption / KMS RDS), en transit (SSL forcé des deux côtés), qui peut lire quoi (Managed Identity / IAM DB auth).
- **Data Migration diagram** — pertinent si des données doivent un jour circuler entre Azure PostgreSQL et AWS RDS.

**Application Architecture — livrables :**
- **Application Portfolio Catalog** — ArkCloud.API, ArkCloud.Blazor, (futur) frontend Angular, (futur) microservices du Sprint 8.
- **Application/Function Matrix** — quelle application sert quelle fonction métier de la Phase B.
- **Application Communication diagram** — Blazor → API, futurs microservices entre eux, ALB/App Service en frontal.
- **Application Architecture Report**.

### Phase D — Technology Architecture

**Livrables :**
- **Technology Standards Catalog** — .NET 10, PostgreSQL 16, Terraform, Docker, Kubernetes (Sprint 9), GitHub Actions — versions et justifications déjà en grande partie présentes dans ce roadmap, à consolider ici.
- **Technology Portfolio Catalog** — App Service/ECS Fargate, Key Vault/Secrets Manager, Log Analytics/CloudWatch — inventaire formel des deux clouds côte à côte.
- **Network/Communications diagram** — VNet Azure + VPC AWS, déjà en grande partie couvert par les diagrammes Graphviz produits pour les états des lieux Sprint 4/5, à reformater au gabarit TOGAF.
- **Platform Decomposition diagram**, **Environments & Locations diagram** — dev/staging/prod × Azure/AWS × régions (westeurope/eu-west-1).

### Phase E — Opportunities & Solutions

**Livrables :**
- **Consolidated Gaps, Solutions & Dependencies Matrix** — tout ce qui est aujourd'hui documenté comme `skip_check` Checkov, TODO Sprint 6+, ou item Step 17 non coché, consolidé formellement.
- **Draft Architecture Roadmap** — quelles capacités cibles (Angular enterprise, microservices, Kubernetes, SRE) dans quel ordre, avec dépendances explicites.
- Identification des **Transition Architectures** — états intermédiaires (ex. "AWS avec RDS mais sans ECS" = état actuel exact au moment d'écrire ce document).

### Phase F — Migration Planning

**Livrables :**
- **Implementation and Migration Plan** (détaillé — coût, bénéfice, risque par incrément) — les Sprints 6 à 10 déjà planifiés devenus des incréments de migration formels.
- **Architecture Roadmap** (finalisée).
- **Architecture Definition Document** (mis à jour avec le contenu des Phases B/C/D).

### Phase G — Implementation Governance

**Livrables :**
- **Architecture Contracts** — accords formels entre l'équipe de développement (même réduite à une personne) et la gouvernance d'architecture sur la conformité de chaque livraison.
- **Compliance Assessments** — vérification que chaque Sprint livré (1 à 10) respecte les Architecture Principles du Préliminaire. Rétroactif pour les Sprints déjà clos.
- **Change Requests**, **Business Value Assessment**.

### Phase H — Architecture Change Management

**Livrables :**
- **Architecture Change Requests** — process formel pour toute évolution future (nouveau cloud, nouveau framework frontend, etc.).
- Critères de décision : quand une évolution reste une simple mise à jour (pas de nouveau cycle ADM) vs quand elle déclenche un retour en Phase A.
- **Architecture Repository** mis à jour en continu à partir d'ici.

### Requirements Management (continu)

**Livrables :**
- **Architecture Requirements Specification** — consolidation de toutes les exigences non-fonctionnelles déjà présentes de façon informelle dans ce projet (audit logging, rotation des secrets, isolation réseau Blazor/PostgreSQL, etc.), formalisées et tracées phase par phase.
- **Requirements Impact Assessment** — à chaque changement futur, évaluation de l'impact sur les exigences déjà tracées.

### Réalignement des documents existants

Dans le cadre de ce sprint, les documents suivants sont réécrits en structure/terminologie TOGAF (contenu factuel conservé, organisation TOGAF-isée) :

- `ArkCloud/README.md` → sections réorganisées pour faire apparaître Application Architecture (Phase C) et Technology Architecture (Phase D).
- `ArkCloud/docs/infra-roadmap.md` (ce document) → devient largement l'**Architecture Roadmap** (Phase E/F) une fois le Sprint 11 clos.
- `ArkCloud/docs/architecture.md` → refondu en **Architecture Definition Document** consolidé (Vision + Business + Data + Application + Technology).
- États des lieux Sprint 1-4 et récits Sprint 4/5 (`.docx`) → deviennent les **Compliance Assessments** rétroactifs (Phase G) : preuve que chaque sprint livré respecte les principes établis au Préliminaire.
- `ArkCloudInfra/README.md` → sections Steps 1-17 réorganisées sous Technology Architecture (Phase D) et Implementation Governance (Phase G).

---

*Ce roadmap garde l'application (`ArkCloud.API`, `ArkCloud.Blazor`, `ArkCloud.Application`, `ArkCloud.Domain`, `ArkCloud.Infrastructure`) séparée des assets de déploiement (`deploy/docker`, `deploy/kubernetes`) dans le repo ArkCloud, et de l'infrastructure cloud (Terraform) dans `ArkCloudInfra`. Il laisse la place pour évoluer d'App Service → AKS/EKS, Docker → Kubernetes, et single-cloud → multi-cloud sans avoir à restructurer les repos plus tard.*
