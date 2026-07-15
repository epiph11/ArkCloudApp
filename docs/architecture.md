# Architecture ArkCloud

## Répartition en deux repos

Ce dépôt (`mon-projet`, équipe produit) contient uniquement le code applicatif :

```
mon-projet/
├── backend/                 (Domain, Application, Infrastructure, API, tests)
├── frontend/                (ArkCloud.Blazor)
└── .github/workflows/
    ├── backend-ci.yml       → build + test + push image Docker (ghcr.io/.../arkcloud-api)
    └── frontend-ci.yml      → build + push image Docker (ghcr.io/.../arkcloud-frontend)
```

L'infrastructure (Terraform, environnements cloud) vit dans un repo séparé, `mon-projet-infra`,
à accès restreint (équipe platform) :

```
mon-projet-infra/
├── modules/
├── environments/{dev,staging,prod}/
└── .github/workflows/
    └── terraform-ci.yml     → plan sur PR, apply sur merge (+ gate manuel pour prod)
```

Note : `ArkCloud.Blazor` est un Blazor **Server** (pas une SPA statique) — son CI build et pousse
une image Docker comme le backend, plutôt qu'un déploiement vers un CDN/bucket statique.

## Couches (backend/)

- **Domain** (`backend/ArkCloud.Domain`) : entités, value objects, exceptions métier, enums. Aucune dépendance technique (pas d'EF, pas d'HTTP).
- **Application** (`backend/ArkCloud.Application`) : DTOs, interfaces de repository/UnitOfWork, validateurs FluentValidation, services applicatifs orchestrant le Domain. Également référencé par `frontend/ArkCloud.Blazor` pour partager les DTOs (contrat HTTP).
- **Infrastructure** (`backend/ArkCloud.Infrastructure`) : `ArkCloudDbContext` (EF Core + PostgreSQL via Npgsql), configurations EF (`IEntityTypeConfiguration<T>`), implémentations des repositories, enregistrement DI.
- **API** (`backend/ArkCloud.API`) : contrôleurs REST versionnés (`/api/v1/...`), middlewares (correlation id, gestion d'erreurs globale en `application/problem+json`), Swagger, Serilog.

## Règles de dépendance

```
Domain <- Application <- Infrastructure <- API
Domain <- Application <- Tests.Unit
                          API <- Tests.Integration
Domain <- Application <- ArkCloud.Blazor (frontend/, DTOs uniquement, jamais Infrastructure)
```

## Entités principales

- `Customer` (Email, Address en value objects)
- `Product` (Money en value object)
- `Order` / `OrderItem` (machine à états : Draft -> Submitted -> Paid, ou Cancelled)

## Gestion des erreurs

Le middleware `ExceptionHandlingMiddleware` mappe :

- `FluentValidation.ValidationException` -> 400
- `ArkCloud.Application.Exceptions.NotFoundException` -> 404
- `ArkCloud.Domain.Exceptions.DomainException` (et `InvalidOrderStateException`) -> 409
- toute autre exception -> 500

Chaque réponse porte un `traceId` correspondant au header `X-Correlation-Id` (généré par `CorrelationIdMiddleware` s'il est absent de la requête).

## Ce qui reste à faire une fois le SDK .NET disponible en local

Ce repo a été généré sans accès à un SDK .NET ni à Internet (voir `README.md`, section "Limites de génération"). Avant la première exécution :

1. `dotnet restore` puis `dotnet build` pour vérifier que tout compile avec les versions de packages réellement résolues.
2. Générer la première migration EF Core (`dotnet ef migrations add InitialCreate ...`) si le dossier `backend/ArkCloud.Infrastructure/Persistence/Migrations` ne contient pas déjà la migration voulue.
3. Remplacer `EnsureCreatedAsync()` par `MigrateAsync()` dans `backend/tests/ArkCloud.Tests.Integration/ArkCloudApiFactory.cs` pour que les tests d'intégration valident le vrai chemin de migration.
