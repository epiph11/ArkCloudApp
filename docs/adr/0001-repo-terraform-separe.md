# ADR-0001 : Terraform dans un repo séparé (`ArkCloudInfra`), pas dans `ArkCloud/deploy/terraform/`

**Statut** : Acceptée
**Date** : 2026-07 (Sprint 4, rétro-documentée Sprint 6)
**Sprint** : 4

## Contexte

La documentation d'origine du projet prévoyait Terraform dans `ArkCloud/deploy/terraform/`, dans le même repo que le code applicatif (monorepo).

## Options envisagées

1. **Monorepo** (`ArkCloud/deploy/terraform/`) — un seul repo à cloner, historique git unifié entre code et infra.
2. **Repo séparé** (`ArkCloudInfra`) — CI/CD, permissions et cycle de vie de version indépendants du code applicatif.

## Décision

Repo séparé (`ArkCloudInfra`), avec la même structure de modules/environnements que celle proposée dans la doc d'origine.

## Conséquences

**Positives**
- Blast radius réduit : un token/rôle CI compromis sur le repo applicatif ne donne pas accès à l'infra, et inversement.
- Gouvernance distincte : une modification d'infra (capable de tout casser en production) suit un chemin de revue différent d'un changement de code applicatif.
- Permissions GitHub scindées par nature de risque plutôt que par commodité de clonage.

**Négatives / compromis assumés**
- Deux repos à synchroniser mentalement — un changement applicatif qui a une conséquence infra (nouvelle variable d'environnement, nouveau secret) demande une PR de chaque côté.
- Le déclenchement cross-repo (nouvelle image → déploiement ciblé, README ArkCloudInfra §7) est un mécanisme supplémentaire à maintenir, qui n'existerait pas en monorepo.

**Ce que ça bloque ou impose pour la suite**
- Toute future automatisation qui traverse les deux repos (ex. rotation `GHCR_PAT`, voir backlog) doit explicitement gérer les deux, pas un seul.
