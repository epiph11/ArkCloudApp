using ArkCloud.Blazor.Auth;
using ArkCloud.Blazor.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;

namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// bUnit context wired with the *real* client-side auth stack (JwtAuthenticationStateProvider,
/// TokenStorageService, ProtectedSessionStorage) so tests exercise the same code path as
/// production. The only thing replaced is the network call, via <see cref="FakeAuthHandler"/> —
/// JS interop (which ProtectedSessionStorage needs) is satisfied by bUnit's own loose-mode
/// IJSRuntime fake, so no Docker/browser/real API is required to run these tests.
/// </summary>
public abstract class AuthTestContext : BunitContext
{
    protected FakeHttpMessageHandler FakeAuthHandler { get; } = new();

    protected AuthTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddDataProtection();
        Services.AddScoped<ProtectedSessionStorage>();
        Services.AddScoped<TokenStorageService>();
        Services.AddScoped(_ => new AuthApiClient(
            new HttpClient(FakeAuthHandler) { BaseAddress = new Uri("https://fake-api.local/") }));
        Services.AddScoped<JwtAuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
        Services.AddAuthorizationCore();
        Services.AddCascadingAuthenticationState();
    }
}
