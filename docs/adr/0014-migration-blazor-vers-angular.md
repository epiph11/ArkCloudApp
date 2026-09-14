# ADR-0014 : Migration du frontend Blazor Server vers Angular — remplacement complet, architecture standalone/Signals, déploiement nginx statique

**Statut** : Acceptée
**Date** : 2026-09-14
**Sprint** : 7

## Contexte

Le frontend `ArkCloud.Blazor` (Blazor Server) couvre aujourd'hui l'authentification (Login/Register), un Dashboard, et le CRUD Customers/Products/Orders — hébergé en Azure App Service et AWS ECS Fargate comme process .NET tenant une connexion SignalR persistante par utilisateur. Le roadmap (`docs/infra-roadmap.md`, `docs/architecture-arkcloud.md`) désigne Sprint 7 "Angular enterprise" comme prochain jalon majeur, sans détail de scope au-delà du titre et de deux notes d'outillage (Compodoc, extension SonarCloud TypeScript). Aucune décision d'architecture Angular (NgRx vs Signals, SSR vs SPA statique, coexistence vs remplacement) n'existait avant cette ADR.

Cette ADR documente les décisions structurantes prises en préparation collaborative de Sprint 7, avant le premier commit de code Angular.

## Options envisagées

**1. Stratégie de transition depuis Blazor**
- **A. Coexistence temporaire** — Angular et Blazor déployés en parallèle, bascule page par page derrière un routeur/proxy. Réduit le risque de régression visible mais double la surface CI/CD/Terraform/Docker pendant toute la transition, et repousse indéfiniment le retrait de Blazor Server (SignalR, coût App Service dédié).
- **B. Remplacement complet** — retenue, voir Décision.

**2. Architecture Angular**
- **A. NgModules + NgRx** — pattern "enterprise" historique, mais NgRx impose une cérémonie (actions/reducers/effects/selectors) disproportionnée pour une appli CRUD de cette taille (4 features), et NgModules est déprécié par l'équipe Angular au profit des standalone components depuis Angular 15+.
- **B. Standalone components + Signals** — retenue, voir Décision.

**3. Modèle de déploiement**
- **A. Angular Universal (SSR)** — pertinent pour du SEO public ou du contenu indexable ; ArkCloud est une appli interne authentifiée, aucun bénéfice SEO. Ajoute un process Node serveur à héberger et faire scaler, à la place d'un simple serveur de fichiers statiques.
- **B. Build statique servi par nginx** — retenue, voir Décision.

## Décision

**Remplacement complet de Blazor**, pas de coexistence. `frontend/ArkCloud.Angular/` est construit en parallèle du Blazor existant le temps du sprint, mais la bascule en production est un événement unique une fois la parité fonctionnelle atteinte (#118) et validée (#119, #122) — pas une migration page-par-page en double exploitation. `frontend/ArkCloud.Blazor/` et `Dockerfile.blazor` sont retirés du repo à la clôture de Sprint 7 (#123), pas seulement désactivés.

**Standalone components + Angular Signals**, pas de NgModules ni NgRx. State géré via des services par feature exposant des `signal()` et leurs mutateurs — suffisant pour la taille de l'appli, sans dépendance externe de state management.

**Déploiement nginx statique** : build Angular en image Docker multi-stage (Node build → nginx alpine/distroless), remplace le modèle "process .NET hébergé" de Blazor Server. Implique côté API : le JWT n'est plus géré par un process serveur avec état (SignalR) mais doit être porté par le navigire ; décision retenue en session de cadrage — cookie httpOnly (Set-Cookie côté `ArkCloud.API`, SameSite/Secure) plutôt que localStorage, pour limiter l'exposition XSS du token (#115, #116).

**Outillage complémentaire retenu en session de cadrage** :
- **openapi-typescript** pour générer le client API depuis le Swagger existant d'`ArkCloud.API` (#117) — évite une dépendance NSwag/.NET dans la chaîne de build Angular.
- **Angular Material** pour l'UI (#118) — cohérent avec l'axe "enterprise" du sprint (accessibilité intégrée, composants de table/formulaire adaptés au CRUD existant), plutôt que Tailwind ou du CSS custom.
- **Jest** (unitaires) + **Playwright** (e2e) pour les tests (#119), Compodoc pour la documentation générée (déjà anticipé au roadmap).
- **En-têtes de sécurité HTTP stricts dès la config nginx initiale** (#120) — HSTS/CSP/Permissions-Policy configurés au premier commit plutôt que découverts et corrigés après un premier scan DAST (#109), pour ne pas rouvrir un cycle de triage `.zap/rules.tsv` déjà fait une fois sur Blazor.
- **Ordre de construction des pages CRUD (#118)** : Dashboard → Customers → Products → Orders — du composant le plus autonome (lecture seule, peu de dépendances) vers le plus dépendant (Orders référence Customers et Products via le `ProductPicker`/`CustomerSearch` existants côté Blazor), pour valider le pattern standalone/Signals sur un cas simple avant de l'appliquer à la page la plus complexe.

## Conséquences

**Positives**
- Une seule pile frontend à maintenir dès la bascule (#123) — pas de double surface CI/CD/Terraform/Docker prolongée dans le temps comme l'aurait imposé l'option coexistence.
- Signals + standalone évite d'introduire une dépendance (NgRx) dont la charge de maintenance n'est pas justifiée par la taille réelle de l'appli (4 features CRUD).
- Cookie httpOnly + en-têtes stricts dès le départ alignent le nouveau frontend sur le niveau de durcissement déjà atteint côté CI/CD et infra en Sprint 6 (SHA-pinning, permissions job-level, DAST, SBOM/Cosign), plutôt que de repartir d'un niveau de sécurité inférieur qu'il faudrait rattraper ensuite.

**Négatives / compromis assumés**
- Le remplacement complet signifie que Blazor et Angular doivent atteindre une parité fonctionnelle complète avant bascule — pas de retrait progressif, donc pas de retour arrière partiel possible une fois #123 exécuté sans revert Git complet.
- Le cookie httpOnly impose une modification d'`ArkCloud.API` (#115) qui n'aurait pas été nécessaire avec un stockage localStorage — coût accepté pour la réduction du risque XSS.
- Aucun SSR : si un besoin de SEO ou de rendu initial rapide apparaît plus tard (peu probable pour une appli interne authentifiée), il faudra revoir cette décision plutôt que l'étendre.

**Ce que ça bloque ou impose pour la suite**
- `arkcloud-frontend-ci.yml` doit être réécrit en profondeur (#121), pas adapté incrémentalement depuis sa version Blazor actuelle (`dotnet build`/`dotnet test` remplacés par `npm`/`jest`/`playwright`).
- Les modules Terraform Azure App Service et AWS ECS Fargate actuels doivent être revérifiés pour confirmer qu'ils acceptent tels quels une image nginx statique (a priori oui — changement d'image seulement) avant tout déploiement réel (#122).
- Toute future décision de state management plus complexe (si l'app grossit significativement au-delà de Sprint 7) devra repartir de Signals, pas de le contourner par une intégration NgRx partielle.
