# Changelog

Format basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/), versionning [SemVer](https://semver.org/lang/fr/).

Ce fichier démarre à `v0.1.0` (Sprint 6) — voir ADR-0009 (`docs/adr/0009-strategie-branches-et-versionning.md`) pour le contexte. L'historique antérieur (Sprints 1 à 5) n'est pas reconstitué commit par commit ici ; il reste détaillé dans `docs/infra-roadmap.md`.

## [Unreleased]

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
