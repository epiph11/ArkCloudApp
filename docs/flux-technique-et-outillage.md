# ArkCloud — Flux technique complet & rôle des outils

> Complément détaillé à `docs/infra-roadmap.md` : où Docker et Kubernetes interviennent exactement dans le projet, et à quel moment chaque outil (Terraform, JFrog, Jenkins, Trivy, etc.) entre en jeu.

---

## 1. Vue d'ensemble

Le flux complet, du poste du développeur jusqu'à la prod, traverse 5 couches :

```
Poste local → CI/CD (build/test/scan) → Registre d'images → Déploiement (Terraform) → Environnement d'exécution
```

Ce qui change au fil des sprints, ce n'est **pas** cette chaîne — c'est la dernière couche : l'environnement d'exécution passe d'App Service (Sprint 4) à ECS Fargate (Sprint 5) puis à Kubernetes (Sprint 9). Docker, lui, reste la même brique du début à la fin.

---

## 2. Poste de développement local

Ce qui tourne sur ta machine, jamais en prod :

| Outil | Rôle |
|---|---|
| .NET SDK 10 | Build/test du backend et du frontend Blazor |
| Docker Desktop | Fait tourner `docker-compose.yml` (API + Blazor + Postgres) pour développer sans dépendance cloud |
| Terraform CLI | `terraform plan`/`apply` locaux avant de laisser la CI le faire, ou pour du debug |
| Azure CLI / AWS CLI | Authentification (`az login`/`aws configure`), inspection manuelle des ressources, bootstrap du state Terraform |
| kubectl / Helm *(Sprint 9)* | Interaction avec le cluster Kubernetes, déploiement de charts, debug de pods |

`docker-compose.yml` (dans `deploy/docker/`) est **uniquement** un outil de développement local — il ne représente aucun environnement staging/prod. Les images qu'il construit ne sont jamais celles qui tournent en cloud ; ce sont des images équivalentes, reconstruites par la CI.

---

## 3. Le rôle de Docker — la brique constante

Docker n'est l'affaire d'aucun sprint en particulier : il traverse tout le roadmap sans changer de nature. Ce qui évolue, c'est qui fait tourner l'image, pas l'image elle-même.

| Sprint | Qui exécute l'image Docker | Ce qui change |
|---|---|---|
| 2 | `docker-compose` en local | Dev uniquement, aucun registre |
| 4 | Azure App Service (Web App for Containers) | La CI construit l'image, la scanne (Trivy), la pousse vers JFrog ; App Service la tire au déploiement |
| 5 | ECS Fargate | Même image, référencée dans une Task Definition ECS au lieu d'une config App Service |
| 9 | Kubernetes (AKS/EKS) | Même image encore, référencée dans un `Deployment` avec replicas/HPA au lieu d'un service géré unique |

**Le `Dockerfile` ne se réécrit jamais entre ces étapes.** Seule la couche qui orchestre le conteneur change : service managé simple (App Service/ECS Fargate) → orchestrateur complet (Kubernetes) quand le besoin de scaling fin, de multi-service ou de déploiements progressifs (blue/green, canary) apparaît.

---

## 4. Le rôle de Kubernetes — Sprint 9, pas avant

**Pourquoi pas avant ?** App Service (Azure) et ECS Fargate (AWS) sont déjà des services de conteneurs managés — pas de node à patcher, pas de control plane à maintenir. Kubernetes n'apporte rien tant que le besoin reste "faire tourner API + Blazor, scaler un peu". Il devient utile quand le Sprint 8 (microservices) a multiplié les services indépendants et que Sprint 9/10 demandent de l'orchestration fine (autoscaling par service, ingress unique, déploiements blue/green).

**Ce que Kubernetes ajoute concrètement à ce moment-là :**

| Ressource K8s | Rôle |
|---|---|
| `namespace.yaml` | Isolation logique par environnement (staging/prod) à l'intérieur du même cluster, ou clusters séparés selon le budget |
| `deployment-api.yaml` / `deployment-blazor.yaml` | Réplicas des conteneurs API et Blazor, rolling updates |
| `service-*.yaml` | Exposition interne des pods (load balancing entre réplicas) |
| `ingress.yaml` | Routage HTTPS externe unique vers les bons services (remplace l'exposition directe d'App Service) |
| `hpa.yaml` | Autoscaling basé sur la conso CPU/mémoire réelle des pods |
| `external-secrets` | Synchronise Key Vault (Azure) / Secrets Manager (AWS) vers de vrais secrets Kubernetes — continuité directe du pattern Key Vault du Sprint 4, pas une nouvelle façon de gérer les secrets |

**Où ça tourne :** AKS ou EKS — cluster managé par le cloud, node pools gérés (pas de VM à faire évoluer soi-même, contrairement à ce qu'aurait nécessité Puppet/Chef). Helm sert à packager les manifests en charts versionnés plutôt que d'appliquer des YAML bruts un par un. `deploy/kubernetes/` reste vide jusqu'à ce sprint — remplir ce dossier avant d'avoir un cluster réel n'aurait rien à tester.

---

## 5. Tableau complet des outils

| Outil | Catégorie | Rôle | Introduit à | Statut |
|---|---|---|---|---|
| GitHub Actions | CI/CD | Build, tests, scan, push d'image, déclenchement des déploiements | Sprint 1 | ✅ Actif |
| Docker / Docker Desktop | Conteneurisation | Unité de packaging de l'app, dev local et déploiement | Sprint 2 | ✅ Actif |
| Terraform | IaC | Provisioning de toute l'infra cloud (Azure puis AWS) | Sprint 4 | 🔄 En cours |
| Azure CLI | Infra | Authentification, bootstrap du state Terraform, debug manuel | Sprint 4 | 🔄 En cours |
| tflint | Sécurité/Qualité IaC | Lint des fichiers Terraform en CI | Sprint 4 | ⏳ À venir |
| Checkov | Sécurité IaC | Scan de configuration Terraform (mauvaises pratiques, failles) | Sprint 4 | ⏳ À venir |
| Trivy | Sécurité conteneurs | Scan de vulnérabilités des images Docker avant push | Sprint 4 | ⏳ À venir |
| JFrog Artifactory | Registre | Registre unique d'images/packages, remplace GHCR, partagé Azure+AWS | Sprint 4/5 | ⏳ À venir |
| AWS CLI | Infra | Équivalent Azure CLI côté AWS | Sprint 5 | ⏳ À venir |
| kubectl | Kubernetes | Interaction manuelle avec le cluster | Sprint 9 | ⏳ À venir |
| Helm | Kubernetes | Packaging des manifests en charts | Sprint 9 | ⏳ À venir |
| Jenkins | CI/CD | Évalué en parallèle de GH Actions pour l'orchestration de déploiements plus riches (blue/green) | Sprint 9 | ⏳ À évaluer (pas un remplacement acté) |

**Volontairement absents du roadmap :** Puppet, Chef — aucune VM longue durée à faire converger dans ce plan (App Service = PaaS, ECS Fargate = serverless, AKS/EKS = nodes managés).

---

## 6. Le flux complet, étape par étape

1. **Dev push** — code poussé sur `ArkCloud` (app) ou `mon-projet-infra` (infra), CI GitHub Actions déclenchée.
2. **Build & tests** — restore/build/test unitaires + intégration (`dotnet test`), côté ArkCloud uniquement.
3. **Build Docker** — image construite en multi-stage (non-root, healthcheck).
4. **Scan Trivy** — vulnérabilités connues bloquantes avant push.
5. **Push vers JFrog Artifactory** — image taguée, disponible pour tous les environnements/clouds.
6. **Terraform (mon-projet-infra)** — `tflint` + `checkov` + `plan` sur PR ; `apply` sur merge, gaté manuellement pour l'environnement prod.
7. **Déploiement** — App Service (Sprint 4) tire la nouvelle image ; ECS Fargate (Sprint 5) ou Kubernetes (Sprint 9) plus tard, sans changer l'image elle-même.
8. **Smoke tests** — vérification post-déploiement automatisée.
9. **Monitoring** — Application Insights (Azure) / CloudWatch (AWS), alimenté en continu par l'environnement qui tourne.

---

## 7. Ce qui ne change jamais vs ce qui évolue

**Constant du Sprint 2 au Sprint 10 :**
- L'image Docker (mêmes Dockerfiles, mêmes couches applicatives)
- GitHub Actions comme CI principale
- Le pattern Key Vault/Secrets Manager pour les secrets (juste un nouveau "consommateur" — App Service, puis ECS, puis K8s via external-secrets)
- Terraform comme unique outil de provisioning (juste plus de modules au fil des sprints)

**Ce qui évolue :**
- L'environnement d'exécution : App Service → ECS Fargate → Kubernetes
- Le registre : GHCR (implicite au départ) → JFrog Artifactory (Sprint 4/5)
- L'orchestrateur de pipeline : GitHub Actions seul → GitHub Actions + Jenkins évalué en parallèle (Sprint 9)
- La complexité réseau : une seule région Azure → multi-cloud Azure+AWS → cluster Kubernetes avec ingress unifié
