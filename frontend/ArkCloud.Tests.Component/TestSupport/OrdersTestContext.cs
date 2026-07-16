using ArkCloud.Blazor.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// bUnit context wired with real Orders/Customers/Products API clients pointed at fake handlers
/// (Orders pages compose CustomerSearch/ProductPicker, which need their own typed clients too),
/// so these pages exercise the same code path as production without a real API/Docker.
/// </summary>
public abstract class OrdersTestContext : BunitContext
{
    protected FakeHttpMessageHandler FakeOrdersHandler { get; } = new();
    protected FakeHttpMessageHandler FakeCustomersHandler { get; } = new();
    protected FakeHttpMessageHandler FakeProductsHandler { get; } = new();

    protected OrdersTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddScoped<ITokenStorageService, FakeTokenStorageService>();
        Services.AddScoped(sp => new OrdersApiClient(
            new HttpClient(FakeOrdersHandler) { BaseAddress = new Uri("https://fake-api.local/") },
            sp.GetRequiredService<ITokenStorageService>()));
        Services.AddScoped(sp => new CustomersApiClient(
            new HttpClient(FakeCustomersHandler) { BaseAddress = new Uri("https://fake-api.local/") },
            sp.GetRequiredService<ITokenStorageService>()));
        Services.AddScoped(sp => new ProductsApiClient(
            new HttpClient(FakeProductsHandler) { BaseAddress = new Uri("https://fake-api.local/") },
            sp.GetRequiredService<ITokenStorageService>()));

        // Roles/authorized state are set per-test via AddAuthorization().SetAuthorized(...)/.SetRoles(...)
        // (see NavMenuTests) — that bUnit helper supplies its own fake AuthenticationStateProvider
        // and cascading auth state, so this context doesn't register a real one.
    }
}
