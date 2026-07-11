# ArkCloud

Backend .NET 10 / PostgreSQL / EF Core suivant une architecture Domain / Application / Infrastructure / API, avec tests unitaires, tests d'intégration, Docker et CI GitHub Actions.

Voir `docs/architecture.md` pour le détail des couches et des règles de dépendance.

## Limites de génération (à lire avant de commencer)

Ce repo a été construit fichier par fichier dans un environnement sandbox qui n'a **ni SDK .NET installé, ni accès réseau sortant** (NuGet, dot.net, GitHub bloqués). Concrètement :

- Aucune commande `dotnet build` / `dotnet test` / `dotnet ef` / `docker build` n'a pu être exécutée pendant la génération. Tout le code (`.csproj`, `.cs`, `.sln`) a été écrit à la main en suivant strictement les conventions `dotnet new` pour .NET 10 et les extraits fournis dans la spec.
- **Aucune migration EF Core n'existe encore** (`src/ArkCloud.Infrastructure/Persistence/Migrations` est vide) : il faut la générer en local (commande ci-dessous).
- Les tests d'intégration utilisent `Database.EnsureCreatedAsync()` plutôt que `MigrateAsync()` en attendant que la migration initiale existe (voir note dans `ArkCloudApiFactory.cs`).
- Toutes les versions de packages NuGet ont été vérifiées par recherche web au moment de la génération (juillet 2026) pour être compatibles .NET 10 : EF Core / EFCore.Design 10.0.9, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, Npgsql 10.0.3, Serilog.AspNetCore 10.0.0, Swashbuckle.AspNetCore 10.2.3, FluentValidation 12.1.1, Microsoft.AspNetCore.Mvc.Testing 10.0.9, Testcontainers.PostgreSql 4.12.0, Microsoft.NET.Test.Sdk 18.7.0, xunit 2.9.3 + xunit.runner.visualstudio 3.1.5, FluentAssertions 7.2.2 (volontairement épinglé en v7 : la v8+ est passée sous licence commerciale Xceed, la v7 reste Apache-2.0 gratuite). Lancez quand même `dotnet restore` en local pour confirmer que rien n'a bougé depuis.
- **`FluentValidation.AspNetCore` est déprécié** (la validation automatique MVC a été retirée du projet FluentValidation). Ce repo ne l'utilise donc pas : chaque contrôleur (`CustomersController`, `OrdersController`, `ProductsController`) injecte son `IValidator<TRequest>` et appelle `ValidateAndThrowAsync` explicitement avant d'appeler le service applicatif — l'exception `FluentValidation.ValidationException` est ensuite convertie en 400 par `ExceptionHandlingMiddleware`, donc le comportement observable (400 + `problem+json`) reste identique à une validation automatique.
- `git init` a été tenté depuis cet environnement mais le dossier `.git` s'est corrompu à cause du pont OneDrive utilisé par la sandbox (le fichier `.git/config` est réécrit avec des octets nuls par le mécanisme de sync). Un dossier `.git` cassé peut donc traîner à la racine : supprimez-le et relancez `git init` depuis votre machine (pas depuis Cowork) :
  ```bash
  rm -rf .git
  git init
  git add -A
  git commit -m "Initial scaffold: ArkCloud .NET 10 backend"
  ```

**Première chose à faire en local : `dotnet build` à la racine, et corriger si besoin.**

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

# 3. Générer puis appliquer la première migration (dossier vide pour l'instant)
dotnet tool install --global dotnet-ef   # si pas déjà installé
dotnet ef migrations add InitialCreate \
  --project src/ArkCloud.Infrastructure \
  --startup-project src/ArkCloud.API \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/ArkCloud.Infrastructure \
  --startup-project src/ArkCloud.API

# 4. Lancer l'API
dotnet run --project src/ArkCloud.API
# Swagger : http://localhost:5080/swagger
```

## Tests

```bash
dotnet test
```

Les tests d'intégration (`tests/ArkCloud.Tests.Integration`) utilisent Testcontainers : Docker doit tourner localement pour qu'ils passent.

## Docker complet (API + PostgreSQL)

```bash
docker compose -f deploy/docker/docker-compose.yml up --build
```

## Endpoints

- `GET/POST /api/v1/customers`, `GET /api/v1/customers/{id}`
- `GET/POST /api/v1/products`, `GET /api/v1/products/{id}`
- `GET/POST /api/v1/orders`, `GET /api/v1/orders/{id}`, `POST /api/v1/orders/{id}/submit`, `POST /api/v1/orders/{id}/cancel`

Toutes les erreurs sont renvoyées en `application/problem+json` avec un `traceId` correspondant au header `X-Correlation-Id`.

## CI

`.github/workflows/backend-ci.yml` : restore, build, test, publish, build de l'image Docker sur chaque push/PR vers `main`/`develop`.

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
