using Amazon;
using Amazon.RDS.Util;
using ArkCloud.Application.Interfaces;
using ArkCloud.Infrastructure.Authentication;
using ArkCloud.Infrastructure.Persistence;
using ArkCloud.Infrastructure.Persistence.Repositories;
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
        if (!string.Equals(authMode, "AwsIam", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            return;
        }

        options.UseNpgsql(BuildAwsIamDataSource(configuration));
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
                Task.FromResult(RDSAuthTokenGenerator.GenerateAuthToken(regionEndpoint, host, port, username)),
            successRefreshInterval: TimeSpan.FromMinutes(10),
            failureRefreshInterval: TimeSpan.FromSeconds(5));

        return builder.Build();
    }
}

