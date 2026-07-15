using ArkCloud.Blazor.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;

namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// bUnit context wired with a real CustomersApiClient pointed at a fake handler, so Customers
/// pages exercise the same typed-client code path as production without a real API/Docker.
/// </summary>
public abstract class CustomersTestContext : BunitContext
{
    protected FakeHttpMessageHandler FakeCustomersHandler { get; } = new();

    protected CustomersTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddDataProtection();
        Services.AddScoped<ProtectedSessionStorage>();
        Services.AddScoped<TokenStorageService>();
        Services.AddScoped(sp => new CustomersApiClient(
            new HttpClient(FakeCustomersHandler) { BaseAddress = new Uri("https://fake-api.local/") },
            sp.GetRequiredService<TokenStorageService>()));

        // Roles/authorized state are set per-test via AddAuthorization().SetAuthorized(...)/.SetRoles(...)
        // (see NavMenuTests) — that bUnit helper supplies its own fake AuthenticationStateProvider
        // and cascading auth state, so this context doesn't register a real one.
    }
}
