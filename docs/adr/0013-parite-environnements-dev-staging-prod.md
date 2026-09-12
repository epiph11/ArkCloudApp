# ADR-0013 : Parité d'environnement dev/staging/prod — paradigme de compute Azure et stratégie de coût

**Statut** : Proposée — aucune décision d'implémentation prise, staging/prod n'existent pas encore
**Date** : 2026-09-12
**Sprint** : discuté en clôture de Sprint 6, candidat Sprint 7+ (avant la construction réelle de `environments/staging`/`environments/prod`)

## Contexte

En cherchant à réduire les dépenses Azure de `dev` (ADR implicite, voir `docs/architecture-arkcloud.md` §Sprint 6), deux changements ont été faits : fusionner les 2 App Service Plans en un seul partagé, et rendre l'environnement destructible/recréable à la demande (`dev-env-up.yml`/`dev-env-down.yml`, ArkCloudInfra) plutôt que de tourner 24/7. Ces deux leviers sont valides pour `dev` précisément parce que `dev` n'a aucun utilisateur réel et un usage sporadique — ils cessent d'être valides tels quels pour `staging`/`prod`, qui doivent rester disponibles.

Cette différence structurelle soulève une question qui dépasse le seul coût : `environments/staging` et `environments/prod` existent déjà comme dossiers vides (`.gitkeep`) dans `ArkCloudInfra`, et les modules Azure/AWS acceptent déjà des variables de taille par environnement (`postgres_sku`, `app_service_sku`, commentaires « B1 pour dev, P1v3+ pour staging/prod » déjà présents dans le code) — mais rien ne dit encore si staging/prod doivent utiliser le **même paradigme de compute** que dev (Azure App Service) ou un autre (ex. Azure Container Apps, évoqué en discussion comme alternative à coût réduit).

## Options envisagées

1. **App Service Plan partout (dev/staging/prod), tailles différentes** — dev reste B1 destructible à la demande, staging/prod passent à un SKU plus gros (P1v3+) avec autoscale et tournent 24/7. Paradigme identique du premier au dernier environnement, seule la taille et le comportement on/off-demand changent.

2. **Container Apps partout (dev/staging/prod)** — migration complète, dev en scale-to-zero natif (sans les workflows `dev-env-up/down.yml`, qui deviendraient inutiles), staging/prod avec un plancher de replicas ≥ 1 pour éviter les cold starts et un autoscale natif au-delà. Facturation à la seconde de vCPU/mémoire réellement consommée plutôt qu'un Plan à taille fixe.

3. **Compute différent par environnement** (ex. App Service pour dev par simplicité/coût immédiat, Container Apps pour prod par optimisation de coût à l'échelle) — rejetée, voir Décision.

## Décision

**Écarter l'option 3.** Le compute doit être le même paradigme sur les trois environnements — seule la taille, le scaling et le comportement on-demand doivent varier. Ni l'option 1 ni l'option 2 n'est actée à ce stade : ce choix reste ouvert et devra être tranché avant de construire `environments/staging`, pas retardé jusqu'à `prod`.

**Raisons d'écarter l'option 3, concrètement :**
- **Parité dev/prod** (principe déjà implicite dans ce projet — Testcontainers pour reproduire Postgres en local plutôt qu'un mock, par exemple) : App Service et Container Apps diffèrent sur des points qui affectent le comportement observable de l'app — modèle de VNet integration, sémantique de health check/readiness, comportement au cold start, mécanisme de TLS. Un bug qui n'existe qu'à cause de cette différence ne serait jamais vu en dev avant d'apparaître en prod — exactement le type de dérive que ce projet évite ailleurs par construction (RGPD, IAM, STRIDE : tout est vérifié, jamais supposé).
- **Coût de maintenance Terraform** : deux modules de compute distincts (`modules/azure/app-service` et un futur `modules/azure/container-app`) à faire évoluer en parallèle, plutôt qu'un seul avec des variables de taille — double la surface à tester et à documenter pour un bénéfice qui n'est pas encore démontré (aucun trafic réel mesuré à ce jour).

**Ce que cette ADR ne tranche pas** : laquelle des options 1 ou 2 adopter. Éléments à peser au moment de la trancher, une fois `staging` en construction :
- Container Apps a un intérêt réel pour ce projet précisément *parce que* le trafic de départ sera faible/imprévisible (facturation à l'usage plutôt qu'une taille fixe dimensionnée "au cas où"), et parce que ça rendrait `dev-env-up.yml`/`dev-env-down.yml` inutiles (scale-to-zero natif, pas d'automation maison à maintenir).
- App Service a l'avantage d'être déjà écrit, testé et documenté (`modules/azure/app-service`, ce Sprint 6) — migrer maintenant vers Container Apps serait un vrai chantier (pas un tweak), à ne pas improviser en fin de sprint à côté du passwordless Azure.
- Si Container Apps est retenu, la migration doit couvrir `dev` aussi (pas seulement staging/prod à venir), précisément pour la raison de parité ci-dessus.

## Conséquences

**Positives**
- Empêche une dérive d'architecture silencieuse (compute différent par environnement) qui aurait été plus coûteuse à défaire une fois `staging`/`prod` construits que maintenant, alors qu'ils n'existent encore que sous forme de dossiers vides.
- Le choix final (option 1 ou 2) reste ouvert et informé par des données réelles (trafic staging observé) plutôt que tranché à l'aveugle en clôture de Sprint 6.

**Négatives / compromis assumés**
- Ne résout rien dans l'immédiat — `staging`/`prod` restent non construits, et cette ADR ne raccourcit pas ce délai. Elle évite juste de prendre la mauvaise décision par défaut (compute mixte) faute d'y avoir réfléchi au bon moment.
- Si Container Apps est retenu plus tard, ça implique de refaire une partie du travail Sprint 6 sur dev (`modules/azure/app-service`, `dev-env-up/down.yml`) plutôt que de le garder tel quel — un coût de ré-plomberie accepté en connaissance de cause, pas découvert après coup.

**Ce que ça bloque ou impose pour la suite**
- Avant tout `terraform apply` créant du contenu réel dans `environments/staging` : trancher explicitement entre l'option 1 et l'option 2 (mise à jour de cette ADR, statut Acceptée), pas juste copier `environments/dev/main.tf` par réflexe.
- Si l'option 2 (Container Apps) est retenue : planifier la migration de `dev` dans le même chantier, pas comme un nettoyage "plus tard" qui ne vient jamais.
