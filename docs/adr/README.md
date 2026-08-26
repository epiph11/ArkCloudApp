# Architecture Decision Records

Format minimal (`template.md`) : contexte, options envisagées, décision, conséquences — **positives et négatives**. Une ADR qui ne liste que des avantages n'est pas honnête ; le compromis assumé est la partie qui a de la valeur pour relire une décision plus tard.

Numérotation séquentielle, jamais réutilisée — une décision remplacée obtient un nouveau numéro et l'ancienne passe au statut "Remplacée par ADR-YYYY", elle n'est pas supprimée.

## Index

| ADR | Titre | Statut |
|---|---|---|
| [0001](0001-repo-terraform-separe.md) | Terraform dans un repo séparé (`ArkCloudInfra`) | Acceptée |
| [0002](0002-registre-images-ecr-provisoire.md) | Amazon ECR provisoire plutôt que JFrog | Acceptée |
| [0003](0003-certificat-auto-signe-alb.md) | Certificat auto-signé sur l'ALB AWS | Acceptée (temporaire) |
| [0004](0004-rotation-secrets-selective.md) | Rotation automatique sélective des secrets | Acceptée |
| [0005](0005-architecture-cible-primaire-dr.md) | Architecture cible Azure/AWS — primaire + DR | Acceptée (migration non commencée) |
| [0007](0007-pat-github-pipeline-risque-accepte.md) | `GHCR_PAT` — risque accepté | Acceptée |
| [0008](0008-rate-limiting-perimetre-applicatif-seul.md) | Rate limiting applicatif seul, rien en infra | Acceptée (temporaire) |
| [0009](0009-strategie-branches-et-versionning.md) | Stratégie de branches et de versionning (trunk-based, SemVer 0.x, Conventional Commits) | Acceptée |

*(0006 réservée — voir `docs/threat-model-stride.md`, la rotation `Jwt:Key` est couverte par l'ADR-0004 plutôt que dupliquée dans une ADR séparée ; le numéro reste sauté plutôt que réattribué, pour ne jamais faire porter à un même numéro deux décisions différentes selon quand on lit ce repo.)*

## Origine

Les décisions 0001-0005 rétro-documentent des choix déjà pris avant que ce format existe (Sprints 4-6) — reconstituées à partir du "Journal des décisions" de `docs/infra-roadmap.md` et des commentaires de code correspondants, pas inventées après coup. Les décisions 0007-0008 sont issues directement de l'analyse STRIDE (`docs/threat-model-stride.md`, Sprint 6) — première fois qu'une ADR est écrite **au moment** de la décision plutôt que reconstituée.

À partir de maintenant (Sprint 6+), toute décision structurante — au sens de la définition du roadmap (Sprint 8 : granularité des services, saga, choix Kafka ; toute décision de résilience/déploiement Sprint 9-10) — est documentée ici au moment où elle est prise.

Ce dossier devient l'entrée de l'Architecture Repository TOGAF au Sprint 11 (Step 19), plutôt qu'un travail de reconstitution séparé à ce moment-là.
