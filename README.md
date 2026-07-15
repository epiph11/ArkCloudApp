# ArkCloud

.NET 10 / PostgreSQL / EF Core, architecture Domain / Application / Infrastructure / API, plus un frontend Blazor Server. Tests unitaires, tests d'intégration, Docker et CI GitHub Actions.

## Prérequis

- .NET SDK 10 (le stack cible est net10.0 partout)
- Docker Desktop
- Git

## Démarrage local

```bash
cd ArkCloud

# 1. Build
dotnet restore
dotnet build

# 2. Base de données locale
docker compose -f deploy/docker/docker-compose.yml up -d postgres

# 3. Générer puis appliquer la migration EF Core si besoin
dotnet tool install --global dotnet-ef   # si pas déjà installé
dotnet ef migrations add InitialCreate `
  --project backend/ArkCloud.Infrastructure `
  --startup-project backend/ArkCloud.API `
  --output-dir Persistence/Migrations

dotnet ef database update `
  --project backend/ArkCloud.Infrastructure `
  --startup-project backend/ArkCloud.API

# 4. Lancer l'API
dotnet run --project backend/ArkCloud.API
# Swagger : http://localhost:5280/swagger
# https://localhost:5281;http://localhost:5280
# dotnet run --project backend/ArkCloud.API --launch-profile https  

# 5. Lancer le frontend Blazor (dans un autre terminal)
dotnet run --project frontend/ArkCloud.Blazor
# https://localhost:7050;http://localhost:5090
# dotnet run --project frontend/ArkCloud.Blazor --launch-profile https

```

## Tests
```bash
dotnet test backend/tests/ArkCloud.Tests.Unit
dotnet test backend/tests/ArkCloud.Tests.Integration
dotnet test frontend/ArkCloud.Tests.Component
```

Les tests d'intégration (`backend/tests/ArkCloud.Tests.Integration`) utilisent Testcontainers : Docker doit tourner localement pour qu'ils passent.

`frontend/ArkCloud.Tests.Component` teste les composants Blazor avec bUnit (`Login`, `LogoutButton`, `NavMenu`) en passant par le vrai `JwtAuthenticationStateProvider`/`TokenStorageService` — seul l'appel HTTP vers l'API est remplacé par un faux handler, donc ni Docker ni une vraie API ne sont nécessaires pour les lancer.

## Docker complet (API + Blazor + PostgreSQL)

```bash
cp deploy/docker/.env.example deploy/docker/.env
# éditer deploy/docker/.env et renseigner JWT_KEY (valeur aléatoire, 64+ caractères)

docker compose -f deploy/docker/docker-compose.yml up --build
```

## Données de test (seed)

```bash
Get-Content deploy/seed/seed_users.sql -Raw | docker exec -i arkcloud-postgres psql -U arkcloud -d arkcloud
Get-Content deploy/seed/seed_catalog_and_orders.sql -Raw | docker exec -i arkcloud-postgres psql -U arkcloud -d arkcloud
psql "Host=localhost;Port=5432;Database=arkcloud;Username=arkcloud;Password=arkcloud" -f deploy/seed/seed_users.sql
psql "Host=localhost;Port=5432;Database=arkcloud;Username=arkcloud;Password=arkcloud" -f deploy/seed/seed_catalog_and_orders.sql
```

Scripts SQL idempotents (ids fixes + `ON CONFLICT DO NOTHING`, ré-exécutables sans risque) :

- `deploy/seed/seed_users.sql` — 50 comptes (3 Admin, 7 Manager, 40 User), mot de passe commun `Sup3rSecret123!`. Couvre les cas métier réels de `AuthService` : compte désactivé, verrouillage actif, verrouillage expiré, tentatives échouées partielles, combinaison désactivé+verrouillé, rôles multiples.
- `deploy/seed/seed_catalog_and_orders.sql` — 20 produits (5 catégories, stocks variés dont rupture à 0), 10 clients, 20 commandes avec order_items couvrant les 4 statuts (Draft/Submitted/Paid/Cancelled).

## Secrets & configuration

**Jamais commités** : clé de signature JWT, mots de passe de base de données, clés d'API, chaînes de connexion. `appsettings.json`/`appsettings.Development.json` ne contiennent que des valeurs vides ou des placeholders.

- **Dev local (`dotnet run`)** : `dotnet user-secrets set "Jwt:Key" "<valeur aléatoire 64+ caractères>" --project backend/ArkCloud.API`. Stocké hors du repo (`%APPDATA%/Microsoft/UserSecrets` ou `~/.microsoft/usersecrets`), voir `UserSecretsId` dans le `.csproj`.
- **Docker Compose** : `deploy/docker/.env` (gitignored, voir `.env.example`) — les conteneurs n'ont pas accès au store `user-secrets` de l'hôte.
- **Production** : `Program.cs` active automatiquement Azure Key Vault dès que `KeyVault:Uri` est configuré (variable d'environnement / App Setting, jamais un fichier commité), via `DefaultAzureCredential` (identité managée en Azure). Les secrets du Key Vault utilisent `--` à la place de `:` (ex. secret `Jwt--Key` → configuration `Jwt:Key`). Tant que `KeyVault:Uri` n'est pas renseigné, ce code est un no-op — aucun Key Vault réel n'existe encore pour ce projet, c'est un pattern prêt à l'emploi.

Pour activer réellement le Key Vault en prod : créer le vault, accorder à l'identité managée de l'App Service le rôle "Key Vault Secrets User", puis définir `KeyVault:Uri`. Aucun changement de code nécessaire.

## Endpoints

- `GET/POST /api/v1/customers`, `GET /api/v1/customers/{id}`
- `GET/POST /api/v1/products`, `GET /api/v1/products/{id}`
- `GET/POST /api/v1/orders`, `GET /api/v1/orders/{id}`, `POST /api/v1/orders/{id}/submit`, `POST /api/v1/orders/{id}/cancel`

Toutes les erreurs sont renvoyées en `application/problem+json` avec un `traceId` correspondant au header `X-Correlation-Id`.

## CI

- `.github/workflows/backend-ci.yml` (déclenché sur `backend/**`) : restore, build, test, publish, build + push de l'image `ghcr.io/.../arkcloud-api` sur push vers `main`/`develop`.
- `.github/workflows/frontend-ci.yml` (déclenché sur `frontend/**`) : restore, build, test (`ArkCloud.Tests.Component`, bUnit), publish, build + push de l'image `ghcr.io/.../arkcloud-frontend` sur push vers `main`/`develop`.

Le Terraform (plan sur PR, apply sur merge, gate manuel pour prod) vit désormais dans le repo séparé `mon-projet-infra`.

## Checklist de vérification (à faire en local, non exécutable depuis cette sandbox)

- [ ] `dotnet build` sans erreur
- [ ] `dotnet test` : tous les tests unitaires et d'intégration passent
- [ ] PostgreSQL tourne (`docker compose up -d postgres`)
- [ ] Migration `InitialCreate` générée et appliquée
- [ ] Swagger répond sur `/swagger`
- [ ] Endpoints customers/products/orders fonctionnels (cf. Postman/Bruno)
- [ ] Erreurs en `problem+json`, header `X-Correlation-Id` présent
- [ ] `docker compose -f deploy/docker/docker-compose.yml up --build` lance API + DB
- [ ] Le workflow GitHub Actions passe au vert sur une PR
