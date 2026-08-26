# ADR-0005 : Architecture cible Azure/AWS — primaire + DR (warm standby), pas actif-actif

**Statut** : Acceptée (cible ; migration non commencée)
**Date** : 2026-08 (post-Sprint 5, rétro-documentée Sprint 6)
**Sprint** : rattachement futur non numéroté — candidat Sprint 11 (TOGAF, Data Architecture)

## Contexte

Depuis le Sprint 5, Azure et AWS sont deux copies parallèles complètes et indépendantes du même stack (bases séparées, secrets/clés JWT séparés, aucun routage ni bascule entre elles). Le roadmap ne définissait aucun état cible pour cette coexistence.

## Options envisagées

1. **Rester parallèle indéfiniment** — pas de bénéfice de résilience réel malgré le coût de faire tourner deux infrastructures complètes ; en cas de panne d'un cloud, rien ne bascule automatiquement vers l'autre.
2. **Actif-actif symétrique** — les deux clouds servent du trafic réel simultanément avec réplication bidirectionnelle. Résilience maximale, mais coût et complexité (cohérence des données à double sens, résolution de conflits) rarement justifiés en pratique en dehors d'une base d'utilisateurs et d'un volume qu'un projet solo n'atteint pas.
3. **Primaire + DR (warm standby)** — un cloud sert tout le trafic, le second tourne à capacité réduite avec réplication continue, prêt à être promu en cas de panne du primaire.

## Décision

Option 3 : primaire + DR, patron warm standby. Trois chantiers identifiés comme bloquants pour cette migration, non commencés à ce jour :
1. Réplication logique PostgreSQL cross-cloud primaire→secondaire (le vrai point dur : la donnée, pas le compute).
2. Couche DNS/health-check agnostique au-dessus des deux clouds (ni Traffic Manager ni Route 53 seuls ne supervisent nativement l'autre cloud).
3. Unification de la clé de signature JWT entre Key Vault et Secrets Manager (sinon un token émis par un cloud devient invalide sur l'autre après bascule).

## Conséquences

**Positives**
- Objectif clair et réaliste pour un projet à cette échelle, cohérent avec la pratique entreprise (actif-actif rarement justifié).
- Le patron warm standby permet de dimensionner le secondaire en dessous du primaire — coût maîtrisable, cohérent avec la discipline budgétaire déjà en place (cost-guard).

**Négatives / compromis assumés**
- Tant que la migration n'a pas commencé, l'état actuel (deux copies sans lien) ne fournit **aucune** capacité de reprise réelle malgré l'apparence de redondance — un risque déjà tracé dans l'analyse de risque (absence de réplication cross-cloud alors que le DR est une cible déclarée).
- RTO/RPO ne peuvent être mesurés qu'une fois la réplication en place — jusque-là, tout chiffre serait théorique, pas expérimental (voir aussi le manque identifié en Step 16 septies : aucun drill de restauration n'a jamais été exécuté).

**Ce que ça bloque ou impose pour la suite**
- Pas de sprint numéroté attribué — candidat naturel pour le travail Data Architecture du Sprint 11 (TOGAF), qui prévoit déjà un Data Migration diagram Azure↔AWS.
- L'unification JWT (bloquant #3) recoupe directement ADR-0004 : `Jwt:Key` doit être résolu (au moins l'unification cross-cloud, sinon le support multi-clés) avant qu'une bascule primaire→DR soit utilisable sans déconnecter tous les utilisateurs.
