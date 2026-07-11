# Architecture ArkCloud

## Couches

- **Domain** (`src/ArkCloud.Domain`) : entités, value objects, exceptions métier, enums. Aucune dépendance technique (pas d'EF, pas d'HTTP).
- **Application** (`src/ArkCloud.Application`) : DTOs, interfaces de repository/UnitOfWork, validateurs FluentValidation, services applicatifs orchestrant le Domain.
- **Infrastructure** (`src/ArkCloud.Infrastructure`) : `ArkCloudDbContext` (EF Core + PostgreSQL via Npgsql), configurations EF (`IEntityTypeConfiguration<T>`), implémentations des repositories, enregistrement DI.
- **API** (`src/ArkCloud.API`) : contrôleurs REST versionnés (`/api/v1/...`), middlewares (correlation id, gestion d'erreurs globale en `application/problem+json`), Swagger, Serilog.

## Règles de dépendance

```
Domain <- Application <- Infrastructure <- API
Domain <- Application <- Tests.Unit
                          API <- Tests.Integration
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
2. Générer la première migration EF Core (`dotnet ef migrations add InitialCreate ...`) — le dossier `src/ArkCloud.Infrastructure/Persistence/Migrations` est vide pour l'instant.
3. Une fois la migration générée, remplacer `EnsureCreatedAsync()` par `MigrateAsync()` dans `tests/ArkCloud.Tests.Integration/ArkCloudApiFactory.cs` pour que les tests d'intégration valident le vrai chemin de migration.
