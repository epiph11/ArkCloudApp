using Amazon;
using Amazon.RDS.Util;
using ArkCloud.Application.Interfaces;
using ArkCloud.Infrastructure.Authentication;
using ArkCloud.Infrastructure.Persistence;
using ArkCloud.Infrastructure.Persistence.Repositories;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArkCloud.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ArkCloudDbContext>(options => ConfigureNpgsql(options, configuration));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ArkCloudDbContext>());

        // Authentication
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }

    // Sprint 6 — passwordless AWS (ADR-0011, scope AWS). Database:AuthMode=AwsIam bascule sur un
    // token IAM RDS (durée de vie 15 min, régénéré toutes les ~10 min) au lieu de la connection
    // string statique lue dans ConnectionStrings:DefaultConnection. Absent ou toute autre valeur
    // => comportement STRICTEMENT inchangé (Azure, tests d'intégration, dev local) : la connection
    // string continue de passer directement à UseNpgsql(string), sans construire de
    // NpgsqlDataSource — un NpgsqlDataSourceBuilder(...).Build() est une opération eager qui
    // s'exécuterait à l'enregistrement du service même quand ArkCloudApiFactory (tests) remplace
    // ensuite ce descriptor par un container Testcontainers ; pas de raison de prendre ce risque
    // en dehors du seul chemin qui en a réellement besoin (AWS IAM).
    //
    // Le rôle admin (arkcloudadmin/master user) n'est volontairement PAS concerné par ce chemin —
    // décision assumée dans l'ADR-0011, hors périmètre de cette implémentation.
    private static void ConfigureNpgsql(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var authMode = configuration["Database:AuthMode"];

        if (string.Equals(authMode, "AwsIam", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(BuildAwsIamDataSource(configuration));
            return;
        }

        // Sprint 6 clôture (12/09) — passwordless Azure (ADR-0011, scope Azure, complète le
        // pendant AWS ci-dessus). Database:AuthMode=AzureAd bascule sur un token Entra ID
        // (durée de vie ~60-90 min côté Azure AD, rafraîchi ici toutes les ~10 min par marge de
        // sécurité, même logique que le token IAM RDS au-dessus) au lieu de la connection string
        // statique. Même garde : absent/autre valeur => chemin ConnectionStrings:DefaultConnection
        // strictement inchangé, aucun NpgsqlDataSourceBuilder(...).Build() eager en dehors du seul
        // mode qui en a besoin.
        if (string.Equals(authMode, "AzureAd", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(BuildAzureAdDataSource(configuration));
            return;
        }

        options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
    }

    private static NpgsqlDataSource BuildAwsIamDataSource(IConfiguration configuration)
    {
        var host = configuration["Database:Host"]
            ?? throw new InvalidOperationException("Database:Host est requis quand Database:AuthMode=AwsIam.");
        var port = int.Parse(configuration["Database:Port"] ?? "5432");
        var database = configuration["Database:Name"]
            ?? throw new InvalidOperationException("Database:Name est requis quand Database:AuthMode=AwsIam.");
        var username = configuration["Database:Username"]
            ?? throw new InvalidOperationException("Database:Username est requis quand Database:AuthMode=AwsIam.");
        // Trouvé en session (10/09/2026) : la surcharge à 3 arguments de GenerateAuthToken résout
        // la région via FallbackRegionFactory (env vars / IMDS), et rien ne garantit que ce
        // mécanisme trouve la bonne région dans une tâche ECS Fargate — un token signé pour la
        // mauvaise région est rejeté par RDS avec exactement le même message générique qu'une
        // permission IAM insuffisante ("PAM authentication failed"), indistinguable côté client
        // (confirmé par la doc AWS re:Post). D'où l'échec réel constaté malgré un GRANT rds_iam et
        // une policy IAM corrects. Fix : région explicite, pas de détection automatique.
        var region = configuration["Database:AwsRegion"]
            ?? throw new InvalidOperationException("Database:AwsRegion est requis quand Database:AuthMode=AwsIam.");
        var regionEndpoint = RegionEndpoint.GetBySystemName(region);

        var builder = new NpgsqlDataSourceBuilder(
            $"Host={host};Port={port};Database={database};Username={username};Ssl Mode=Require");

        // RDSAuthTokenGenerator.GenerateAuthToken signe la requête en SigV4 localement (aucun
        // appel réseau) — les credentials sont résolues via la chaîne standard du SDK AWS, donc
        // automatiquement celles du task role ECS en conteneur (voir modules/aws/ecs/main.tf).
        // successRefreshInterval < 15 min (durée de vie réelle du token) par marge de sécurité,
        // même logique que le commentaire du token Entra ID côté proposition Azure de l'ADR-0011.
        builder.UsePeriodicPasswordProvider(
            passwordProvider: (_, _) =>
                new ValueTask<string>(RDSAuthTokenGenerator.GenerateAuthToken(regionEndpoint, host, port, username)),
            successRefreshInterval: TimeSpan.FromMinutes(10),
            failureRefreshInterval: TimeSpan.FromSeconds(5));

        return builder.Build();
    }

    // Scope OSS RDBMS fixe documenté par Microsoft pour Postgres/MySQL Flexible Server — pas de
    // variante par ressource comme les scopes ARM classiques (".default" sur une audience de
    // service, pas sur une resource ID précise).
    private static readonly string[] AzurePostgresTokenScopes = ["https://ossrdbms-aad.database.windows.net/.default"];

    private static NpgsqlDataSource BuildAzureAdDataSource(IConfiguration configuration)
    {
        var host = configuration["Database:Host"]
            ?? throw new InvalidOperationException("Database:Host est requis quand Database:AuthMode=AzureAd.");
        var port = int.Parse(configuration["Database:Port"] ?? "5432");
        var database = configuration["Database:Name"]
            ?? throw new InvalidOperationException("Database:Name est requis quand Database:AuthMode=AzureAd.");
        // Doit être exactement le nom de rôle Postgres créé par pgaadauth_create_principal côté
        // serveur (voir modules/azure/postgresql/main.tf et
        // docs/runbooks/bootstrap-arkcloud-app-azure-entra-id.md) — PAS l'Object ID de l'identité
        // managée. Azure AD auth sur Flexible Server authentifie par ce nom de rôle + le token
        // porté comme mot de passe, la correspondance nom↔identité est faite côté serveur au
        // moment de la création du principal, pas à la connexion.
        var username = configuration["Database:Username"]
            ?? throw new InvalidOperationException("Database:Username est requis quand Database:AuthMode=AzureAd.");

        var builder = new NpgsqlDataSourceBuilder(
            $"Host={host};Port={port};Database={database};Username={username};Ssl Mode=Require");

        // DefaultAzureCredential — Managed Identity en environnement Azure réel (App Service,
        // system-assigned, même identité que celle déjà utilisée pour Key Vault dans ce projet),
        // retombe sur Azure CLI / Visual Studio en local si jamais quelqu'un doit tester ce chemin
        // hors App Service. Aucun credential stocké, même logique que le chemin Key Vault existant
        // (Program.cs, ArkCloud.API).
        var credential = new DefaultAzureCredential();

        builder.UsePeriodicPasswordProvider(
            passwordProvider: async (_, cancellationToken) =>
            {
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(AzurePostgresTokenScopes),
                    cancellationToken);
                return token.Token;
            },
            successRefreshInterval: TimeSpan.FromMinutes(10),
            failureRefreshInterval: TimeSpan.FromSeconds(5));

        return builder.Build();
    }
}

