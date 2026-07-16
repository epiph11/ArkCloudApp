using ArkCloud.Blazor.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// bUnit context wired with a real ProductsApiClient pointed at a fake handler, so Products
/// pages exercise the same typed-client code path as production without a real API/Docker.
/// </summary>
public abstract class ProductsTestContext : BunitContext
{
    protected FakeHttpMessageHandler FakeProductsHandler { get; } = new();

    protected ProductsTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddScoped<ITokenStorageService, FakeTokenStorageService>();
        Services.AddScoped(sp => new ProductsApiClient(
            new HttpClient(FakeProductsHandler) { BaseAddress = new Uri("https://fake-api.local/") },
            sp.GetRequiredService<ITokenStorageService>()));

        // Roles/authorized state are set per-test via AddAuthorization().SetAuthorized(...)/.SetRoles(...)
        // (see NavMenuTests) — that bUnit helper supplies its own fake AuthenticationStateProvider
        // and cascading auth state, so this context doesn't register a real one.
    }
}
