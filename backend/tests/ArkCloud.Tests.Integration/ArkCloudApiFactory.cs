using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs.Auth;
using ArkCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        // Program.cs refuses to start without Jwt:Key, and appsettings.Development.json no
        // longer carries one (that was a committed secret — see README's "Secrets &
        // configuration" section). This is a fixture value for ephemeral, Testcontainers-backed
        // test runs only, not a real secret, so it's fine to keep it here in source control —
        // it's the same category of thing as the Testcontainers Postgres password below.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-test-fixture-key-not-a-real-secret-0123456789ABCDEF"
            });
        });

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

    /// <summary>
    /// Every business endpoint is behind [Authorize] as of Phase 2A. Registers a fresh
    /// ("User" role) account and returns an HttpClient with the resulting access token
    /// already attached, so existing endpoint tests keep exercising the real auth flow
    /// instead of bypassing it.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = $"test.{Guid.NewGuid():N}@example.com",
            Password = "Sup3r$ecretPwd!1",
            FirstName = "Test",
            LastName = "User"
        });

        register.EnsureSuccessStatusCode();

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>
    /// Same as <see cref="CreateAuthenticatedClientAsync()"/>, but promotes the fresh account to
    /// <paramref name="roleName"/> (e.g. RoleNames.Admin) directly via the DbContext — there is no
    /// role-promotion endpoint yet — then re-authenticates so the role claim lands in the JWT.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string roleName)
    {
        var email = $"test.{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecretPwd!1";

        var client = CreateClient();

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email,
            Password = password,
            FirstName = "Test",
            LastName = "User"
        });

        register.EnsureSuccessStatusCode();

        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ArkCloudDbContext>();

            var user = await context.Users.Include(u => u.UserRoles)
                .FirstAsync(u => u.Email == email.ToLowerInvariant());
            var role = await context.Roles.FirstAsync(r => r.Name == roleName);

            user.AssignRole(role);
            await context.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }
}
