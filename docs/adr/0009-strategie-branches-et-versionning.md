# ADR-0009 : Stratégie de branches et de versionning

**Statut** : Acceptée
**Date** : 2026-08-26 (Sprint 6)
**Sprint** : 6

## Contexte

Aucun système de versionning n'a jamais existé sur ce projet : ni tag Git, ni `CHANGELOG.md`, ni convention de commit. Tout le travail réel (Sprint 1 à 6) vit sur `develop` ; `main` n'a jamais reçu que le commit de scaffold initial et n'a jamais été mise à jour depuis. Une branche fantôme `Development` (majuscule) traînait aussi sur le remote — vérifiée sans commit unique par rapport à `develop`, supprimée sans perte.

Le roadmap prévoit déjà `environments/staging` et `environments/prod` comme travail futur (Sprint 9-10) — poser une convention de branches/versions maintenant coûte peu ; la retrofitter après Sprint 7 (Angular) et Sprint 8 (microservices), avec plus de code et plus d'historique, coûterait bien plus cher.

## Options envisagées

1. **GitFlow complet** (`main`/`develop`/`release/*`/`hotfix/*`) — modèle standard pour des logiciels à cycles de release espacés avec plusieurs versions en production simultanément. Plus de cérémonie (branches supplémentaires à gérer, merges croisés) que nécessaire pour un projet à un seul environnement déployé aujourd'hui.
2. **Trunk-based allégé / GitHub Flow** — `develop` comme branche d'intégration continue (déjà l'usage réel), `main` protégée ne recevant que des merges depuis `develop` quand un point est jugé stable. Moins de cérémonie, aligné avec le déploiement continu déjà en place (CI sur `develop`).
3. **Ne rien changer** — continuer sans convention. Écarté : c'est précisément ce qui a produit la situation actuelle (aucune trace de version, `main` abandonnée).

## Décision

Option 2. Concrètement :

- **`develop`** reste la branche d'intégration — tout le travail continue de s'y passer comme aujourd'hui.
- **`main`** devient protégée (PR obligatoire, CI verte requise avant merge — à activer dans les paramètres GitHub du repo) et représente l'état "déployable" du projet. Alimentée par merge depuis `develop` à la clôture d'un sprint ou d'un jalon jugé stable, pas à chaque commit.
- **SemVer** (`MAJOR.MINOR.PATCH`), premier tag **`v0.1.0`** posé à la clôture du Sprint 6 — le `0.x` de SemVer signifie explicitement "aucune garantie de stabilité/compatibilité", ce qui correspond exactement à l'état réel du projet. Passage à `v1.0.0` : décision consciente future, pas une formalité automatique, quand le projet atteint un état jugé réellement production-ready (probablement après Sprint 10/11 vu le roadmap : DR, SRE, alignement TOGAF).
- **Conventional Commits** (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`...) pour les messages de commit à partir de maintenant — l'historique existant n'est pas réécrit rétroactivement.
- **Génération manuelle** du tag et du `CHANGELOG.md` pour l'instant, pas d'automatisation (semantic-release/release-please) tout de suite — cohérent avec la discipline déjà appliquée à ArchUnitNET/infracost : ne pas ajouter un outil de plus avant d'avoir confirmé la valeur du process manuel en pratique.

## Conséquences

**Positives**
- `main` redevient un repère fiable ("ce qui tourne réellement"), au lieu d'être un vestige oublié.
- Un historique de versions lisible démarre maintenant plutôt que d'être reconstitué a posteriori (plus dur, moins fiable).
- Le `0.x` de SemVer communique honnêtement l'état du projet — pas de fausse promesse de stabilité.

**Négatives / compromis assumés**
- Processus manuel (tag + CHANGELOG) = discipline à maintenir à chaque clôture de sprint ; aucun garde-fou automatique si on l'oublie une fois.
- Pas de branches `release/*` : si jamais deux versions doivent être maintenues en parallèle (ex. un correctif urgent sur une version pendant que `develop` a déjà avancé), ce modèle n'a pas de réponse toute faite — à revoir si ce cas se présente réellement.

**Ce que ça bloque ou impose pour la suite**
- Activer la protection de branche sur `main` dans les paramètres GitHub (action utilisateur, pas automatisable depuis cet environnement).
- Chaque clôture de sprint à partir de maintenant : merge `develop` → `main`, tag SemVer, entrée `CHANGELOG.md` — sinon la convention se délite comme le reste avant elle.
- Revisiter cette ADR si un besoin de maintenir plusieurs versions en parallèle apparaît (probablement pas avant un vrai environnement de prod avec des utilisateurs réels).
