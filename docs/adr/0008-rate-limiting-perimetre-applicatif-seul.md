# ADR-0008 : Rate limiting au périmètre applicatif seul, rien au niveau ALB/App Service

**Statut** : Acceptée (temporaire)
**Date** : 2026-08 (Sprint 6, issue de l'analyse STRIDE)
**Sprint** : 6

## Contexte

`ArkCloud.API` implémente déjà un rate limiter ASP.NET Core (`AddRateLimiter`, fenêtre fixe par IP source, principalement scopé à l'endpoint de login via `RateLimiting:LoginWindowSeconds`/`LoginPermitLimit`). Aucun mécanisme équivalent n'existe au niveau infrastructure (ALB AWS, App Service Azure) — une requête est déjà arrivée jusqu'au compute avant que la moindre limite s'applique.

Voir `docs/threat-model-stride.md` (flux navigateur→ALB/App Service, Denial of service).

## Options envisagées

1. **AWS WAF devant l'ALB** (rate-based rules) — protection en amont réelle côté AWS, mais nouveau composant infra + coût (Checkov `CKV2_AWS_28`, déjà écarté au Sprint 6 pour cette même raison, catégorisé comme durcissement Sprint 6+ non fait).
2. **Azure Front Door / Application Gateway** devant App Service — équivalent côté Azure, infra nouvelle non existante aujourd'hui (`CKV_AZURE_222`, App Service en accès public direct, déjà documenté comme dépendant d'un point d'entrée public qui n'existe pas encore).
3. **Rester sur le rate limiting applicatif seul**, en assumant explicitement la limite.

## Décision

Option 3 pour l'instant : aucune protection au niveau infra n'est construite ce sprint. Le rate limiting applicatif existant reste la seule ligne de défense contre un abus de trafic — efficace contre un abus applicatif ciblé (ex. brute-force login), inefficace contre un déni de service volumétrique qui sature la capacité avant même d'atteindre la logique applicative.

## Conséquences

**Positives**
- Aucun coût ni infra supplémentaire ce sprint, cohérent avec le budget cost-guard (7 €/mois) déjà serré.
- La protection applicative existante couvre le cas d'usage le plus probable à ce stade (abus ciblé sur l'authentification), pas un scénario de déni de service à grande échelle peu vraisemblable contre un projet à ce trafic.

**Négatives / compromis assumés**
- Un déni de service volumétrique réel (au-delà du login, ou en volume suffisant pour saturer la capacité compute) n'est filtré par rien avant d'atteindre l'application.
- La décision recoupe deux findings Checkov déjà écartés (`CKV2_AWS_28` côté AWS, `CKV_AZURE_222` côté Azure) — cette ADR les relie explicitement à une décision de risque assumée plutôt que deux suppressions isolées sans lien apparent.

**Ce que ça bloque ou impose pour la suite**
- Revoir cette décision si un point d'entrée public unifié (Application Gateway/Front Door côté Azure, ou WAF côté AWS) devient nécessaire pour d'autres raisons — à ce moment-là, le rate limiting en amont devient un ajout marginal plutôt qu'un projet dédié.
