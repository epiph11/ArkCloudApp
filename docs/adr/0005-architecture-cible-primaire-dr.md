# ADR-0005 : Architecture cible Azure/AWS — primaire + DR (warm standby), pas actif-actif

**Statut** : Acceptée (cible ; migration non commencée)
**Date** : 2026-08 (post-Sprint 5, rétro-documentée Sprint 6) — rattachement sprint précisé le 08/09/2026
**Sprint** : réparti Sprints 12 et 13 (voir Décision et Conséquences) plutôt qu'un candidat unique Sprint 11 comme envisagé initialement

## Contexte

Depuis le Sprint 5, Azure et AWS sont deux copies parallèles complètes et indépendantes du même stack (bases séparées, secrets/clés JWT séparés, aucun routage ni bascule entre elles). Le roadmap ne définissait aucun état cible pour cette coexistence.

## Options envisagées

1. **Rester parallèle indéfiniment** — pas de bénéfice de résilience réel malgré le coût de faire tourner deux infrastructures complètes ; en cas de panne d'un cloud, rien ne bascule automatiquement vers l'autre.
2. **Actif-actif symétrique** — les deux clouds servent du trafic réel simultanément avec réplication bidirectionnelle. Résilience maximale, mais coût et complexité (cohérence des données à double sens, résolution de conflits) rarement justifiés en pratique en dehors d'une base d'utilisateurs et d'un volume qu'un projet solo n'atteint pas.
3. **Primaire + DR (warm standby)** — un cloud sert tout le trafic, le second tourne à capacité réduite avec réplication continue, prêt à être promu en cas de panne du primaire.

## Décision

Option 3 : primaire + DR, patron warm standby. Trois chantiers identifiés comme bloquants pour cette migration, non commencés à ce jour :
1. Réplication logique PostgreSQL cross-cloud primaire→secondaire (le vrai point dur : la donnée, pas le compute). **Rattaché au Sprint 12** (Plateforme données & analytics, Step 16 decies du roadmap) — sujet donnée avant tout, cohérent avec le contenu déjà prévu là (Synapse Link, Data Lake).
2. Couche DNS/health-check agnostique au-dessus des deux clouds (ni Traffic Manager ni Route 53 seuls ne supervisent nativement l'autre cloud). **Rattaché au Sprint 13** (Edge, réseau global & résilience multi-cloud, Step 16 undecies du roadmap) — Traffic Manager y est déjà prévu, c'est le même chantier.
3. Unification de la clé de signature JWT entre Key Vault et Secrets Manager (sinon un token émis par un cloud devient invalide sur l'autre après bascule). **Reste un item indépendant, non rattaché à un sprint numéroté** — recoupe directement ADR-0004 (gestion de `Jwt:Key`) plutôt qu'un sujet infra/réseau ; à traiter comme extension de cette décision existante quand la bascule primaire→DR devient concrète.

## Conséquences

**Positives**
- Objectif clair et réaliste pour un projet à cette échelle, cohérent avec la pratique entreprise (actif-actif rarement justifié).
- Le patron warm standby permet de dimensionner le secondaire en dessous du primaire — coût maîtrisable, cohérent avec la discipline budgétaire déjà en place (cost-guard).

**Négatives / compromis assumés**
- Tant que la migration n'a pas commencé, l'état actuel (deux copies sans lien) ne fournit **aucune** capacité de reprise réelle malgré l'apparence de redondance — un risque déjà tracé dans l'analyse de risque (absence de réplication cross-cloud alors que le DR est une cible déclarée).
- RTO/RPO ne peuvent être mesurés qu'une fois la réplication en place — jusque-là, tout chiffre serait théorique, pas expérimental (voir aussi le manque identifié en Step 16 septies : aucun drill de restauration n'a jamais été exécuté).

**Ce que ça bloque ou impose pour la suite**
- Répartition précisée le 08/09/2026 : bloquants #1 et #2 rattachés respectivement aux Sprints 12 et 13 (backlog services Azure avancés, voir journal de décisions du roadmap) plutôt qu'à un unique candidat Sprint 11 comme envisagé initialement. Le Sprint 11 (TOGAF, Data Architecture) reste pertinent pour documenter formellement la cible une fois les trois chantiers avancés, mais n'est plus la seule porte d'entrée du sujet.
- L'unification JWT (bloquant #3) recoupe directement ADR-0004 : `Jwt:Key` doit être résolu (au moins l'unification cross-cloud, sinon le support multi-clés) avant qu'une bascule primaire→DR soit utilisable sans déconnecter tous les utilisateurs.
- Aucun des trois chantiers n'est encore planifié à une date précise — les Sprints 12/13 sont eux-mêmes en backlog, pas engagés.
