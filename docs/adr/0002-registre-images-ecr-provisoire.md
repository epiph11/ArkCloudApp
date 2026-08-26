# ADR-0002 : Amazon ECR provisoire plutôt que JFrog Artifactory comme registre d'images

**Statut** : Acceptée
**Date** : 2026-08 (Sprint 5, rétro-documentée Sprint 6)
**Sprint** : 5

## Contexte

La doc d'origine ne spécifiait pas de registre explicitement (JFrog Artifactory sous-entendu). GHCR (GitHub Container Registry) sert déjà Azure App Service depuis le Sprint 4. Le Sprint 5 introduit AWS ECS, qui a besoin d'un registre accessible depuis le VPC AWS — GHCR ne s'intègre pas nativement avec les rôles IAM ECS de la même façon qu'ECR.

## Options envisagées

1. **JFrog Artifactory** (prévu par la doc d'origine) — registre unique multi-cloud, mais outil externe à opérationnaliser (hébergement, licence, intégration CI) avant d'en avoir un besoin réel.
2. **Amazon ECR** — natif ECS, intégration IAM directe (rôle CI scopé au repository, pas de PAT), zéro friction pour le Sprint 5, mais un deuxième registre en parallèle de GHCR plutôt qu'un registre unique.

## Décision

Amazon ECR, provisoire — sans date fixée pour introduire JFrog. GHCR continue de servir Azure en parallèle.

## Conséquences

**Positives**
- Aucun outil externe à opérationnaliser pour livrer le Sprint 5 dans les temps.
- Authentification ECR entièrement via rôle IAM (OIDC), même discipline que le reste de l'infra AWS — pas de PAT équivalent à `GHCR_PAT` côté ECR.

**Négatives / compromis assumés**
- Le registre n'est pas unique multi-cloud, contrairement à l'intention d'origine : deux registres (GHCR pour Azure, ECR pour AWS) à maintenir et surveiller (CVE, rétention, coûts) séparément.
- `GHCR_PAT` reste un point de friction (rotation manuelle, voir backlog Sprint 6) précisément parce que GHCR n'a pas d'équivalent du rôle IAM qu'ECR permet.

**Ce que ça bloque ou impose pour la suite**
- Si JFrog est introduit plus tard, ça implique une migration des deux registres vers un seul plutôt qu'un ajout — pas planifié sur un sprint pour l'instant.
