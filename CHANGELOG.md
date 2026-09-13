# Changelog

Format basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/), versionning [SemVer](https://semver.org/lang/fr/).

Ce fichier démarre à `v0.1.0` (Sprint 6) — voir ADR-0009 (`docs/adr/0009-strategie-branches-et-versionning.md`) pour le contexte. L'historique antérieur (Sprints 1 à 5) n'est pas reconstitué commit par commit ici ; il reste détaillé dans `docs/infra-roadmap.md`.

## [Unreleased]

## [0.3.0] - 2026-09-13

Clôture réelle du Sprint 6 : authentification passwordless Azure Entra ID opérée en conditions réelles (le volet resté "Proposée" en v0.2.0), et backlog sécurité zéro-coût.

### Added
- Authentification passwordless Azure (Entra ID) pour `arkcloud_app` — `DefaultAzureCredential` via l'identité managée système de l'App Service, vérifiée par un test de login réel (`401 Invalid email or password`, pas un `500`). ADR-0011 passe "Acceptée et implémentée" pour les deux clouds (AWS + Azure). Voir `docs/runbooks/bootstrap-arkcloud-app-azure-entra-id.md` et `docs/etat-des-lieux-sprint6-final.md` §5.2 pour le détail complet, bug par bug.
- Images Docker distroless : `Dockerfile.blazor` basculé sur `aspnet:10.0-noble-chiseled` (sans shell ni gestionnaire de paquets). `Dockerfile.api` gardé sur l'image standard, exception documentée (sshd Kudu nécessaire à la rotation `arkcloud_app`).
- Secret-scanning pre-commit (`gitleaks`, framework `pre-commit`) sur `ArkCloudApp` et `ArkCloudInfra` — bloque un commit contenant un secret avant même le push.
- Scan DAST (OWASP ZAP baseline) contre l'API et le Blazor réellement déployés en dev (`dast-scan.yml`) — 0 finding High au premier passage, dette Medium/Low triée dans `.zap/rules.tsv`.
- Détection de drift Terraform (`drift-detection.yml`, ArkCloudInfra) — `terraform plan` hebdomadaire en lecture seule, échoue si le state diverge de la réalité.

### Fixed
- `deploy-on-image.yml` (ArkCloudInfra) échouait sur tout déploiement d'image API/Blazor depuis l'ajout des variables `entra_admin_principal_name`/`entra_admin_object_id` — oubliées dans ce workflow (présentes ailleurs). Corrigé.
- `ArkCloud.API.csproj` référençait `Azure.Identity` en version inférieure à celle exigée transitivement par `ArkCloud.Infrastructure.csproj` (NU1605, bloquait `dotnet ef migrations script`). Aligné sur 1.14.2.
- Base de données PostgreSQL Azure `arkcloud` : les migrations EF Core n'avaient jamais été appliquées (trou pré-existant, sans rapport avec ce sprint) — appliquées via script généré localement.
- Permissions `arkcloud_app` sur les tables nouvellement créées (`42501: permission denied`) — `ALTER DEFAULT PRIVILEGES` ne s'applique qu'aux objets créés par le rôle exact qu'il cible ; re-`GRANT` explicite ajouté.
- Terraform Azure : changement de Plan App Service impossible tant que la Regional VNet Integration est active — toggle `disconnect_vnet_for_plan_migration` ajouté pour un apply en 2 phases. Ce même correctif a permis de fusionner les deux App Service Plans en un seul (~12€/mois économisés).

## [0.2.0] - 2026-09-12

Suite de clôture Sprint 6 : sécurisation de la chaîne de livraison (SBOM, signature d'images, analyse statique, scan de vulnérabilités, mises à jour automatisées de dépendances) et conformité RGPD (droit à l'effacement corrigé, purge automatisée).

### Added
- Rotation `arkcloud_app` sur Azure via Kudu SSH, vérifiée en conditions réelles (ADR-0010).
- Authentification passwordless AWS (IAM DB auth) pour `arkcloud_app`, vérifiée en production ; volet Azure (Entra ID) resté à l'état de proposition (ADR-0011).
- SBOM (Syft, format SPDX) généré sur le digest exact de chaque image poussée, signature Cosign keyless (OIDC GitHub) et attestation du SBOM — backend et frontend.
- Purge RGPD automatisée des clients inactifs (3 ans, dernière commande, anonymisation) — mécanisme asymétrique par cloud : `BackgroundService` in-process côté Azure (`ArkCloud.API`), Lambda planifiée EventBridge côté AWS (`modules/aws/gdpr-purge`), déployée et vérifiée en Terraform (ADR-0012).
- SonarCloud activé (analyse CI-based sur `arkcloud-backend-ci.yml`), vérifié sur un run réel.
- Snyk activé (scan de dépendances .NET), vérifié sur un run réel ; scan d'image conteneur câblé, pas encore vérifié sur un build réel.
- Renovate installé sur `ArkCloudApp` et `ArkCloudInfra` (patchs NuGet/npm auto-mergés, Terraform et alertes de vulnérabilité jamais auto-mergés).

### Fixed
- `orders.customer_id` sans contrainte de clé étrangère — commandes orphelines possibles après suppression d'un client. Contrainte FK (`ON DELETE RESTRICT`) ajoutée ; suppression d'un client avec commandes remplacée par une anonymisation plutôt qu'un hard delete, pour rester cohérent avec l'exception d'obligation légale du RGPD (art. 17(3)(b)).
- Bug de pipeline : plusieurs `git push` avaient visé le mauvais dépôt (`ArkCloudApp` vs `ArkCloudInfra`), laissant un fix déjà écrit jamais réellement déployé malgré des `terraform apply` répétés côté infra.

### Known issues
- Procédure d'effacement RGPD formelle (demande réelle de bout en bout, y compris délai de survie en backup) jamais testée.
- Premier job Renovate pas encore observé sur les deux repos (installation trop récente).
- Zip de la Lambda `gdpr-purge` construit localement via un script PowerShell équivalent (`build.ps1`), faute de bash fonctionnel sur la machine — pas garanti octet-pour-octet identique au `build.sh` bash utilisé par la CI.

## [0.1.0] - 2026-08-26

Premier tag du projet. `0.x` signifie explicitement : aucune garantie de stabilité ou de compatibilité, le projet est encore en construction active (voir ADR-0009).

### Added
- Backend .NET 10 (Domain / Application / Infrastructure / API), auth JWT, Blazor Server (Sprints 1-3).
- CI/CD GitHub Actions + infrastructure Azure (App Service, PostgreSQL Flexible Server, Key Vault) et AWS (VPC, ECS Fargate, RDS PostgreSQL, ALB) en parallèle (Sprints 4-5).
- Durcissement sécurité cloud (Sprint 6) : NSG flow logs, HTTPS/ACM sur l'ALB, rotation automatique des secrets Postgres (Azure Automation Runbook + AWS Secrets Manager), GuardDuty, Microsoft Defender for Cloud, audit et resserrement IAM moindre-privilège (Azure + AWS).
- Fitness functions : ArchUnitNET (règles de couche Clean Architecture, exécutées en CI) et infracost (garde-fou de coût sur les PR Terraform).
- Modélisation de menaces STRIDE (`docs/threat-model-stride.md`) et classification RGPD des données (`docs/rgpd-classification-donnees.md`), avec correction du seul cas réel trouvé de donnée personnelle en clair dans les logs applicatifs (email dans `AuthService`, remplacé par l'id utilisateur).
- Format ADR adopté (`docs/adr/`) — 9 décisions documentées, rétroactives (0001-0005) et prises en direct (0007-0009).
- Stratégie de branches et de versionning (cette entrée même) — voir ADR-0009.

### Fixed
- `ResideInAssembly(string)` d'ArchUnitNET matchait silencieusement zéro type (bug réel trouvé au premier run local, corrigé en passant l'objet `Assembly` directement).
- Formatage des prix dépendant de la culture du thread (`30,00` au lieu de `30.00` en locale française) — forcé en `CultureInfo.InvariantCulture` sur les 13 occurrences trouvées.
- Rôle IAM CI AWS (`arkcloudinfra-ci`) : `AdministratorAccess` retiré, remplacé par deux policies scopées compte/région.

### Known issues
- Vulnérabilité modérée `NU1902` sur la dépendance de test `AngleSharp` 1.4.0 — reportée volontairement à l'installation de Snyk/Renovate plutôt que corrigée isolément.
- Trois menaces STRIDE encore "à traiter" (logs d'accès ALB, revue des privilèges SQL applicatifs, restaurabilité après rotation) — voir résumé de `docs/threat-model-stride.md`.
