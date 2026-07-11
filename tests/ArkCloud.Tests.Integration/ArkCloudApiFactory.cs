using ArkCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace ArkCloud.Tests.Integration;

public class ArkCloudApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("arkcloud_test")
        .WithUsername("arkcloud")
        .WithPassword("arkcloud")
        .Build();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ArkCloudDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<ArkCloudDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArkCloudDbContext>();

        // NOTE: this uses EnsureCreatedAsync (builds the schema straight from the EF model)
        // instead of MigrateAsync, because no EF migration exists yet in this repo — it must
        // be generated once locally with `dotnet ef migrations add InitialCreate` (see README).
        // Once a migration exists, switch this back to context.Database.MigrateAsync() so the
        // tests exercise the same migration path used in production.
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
