using ArkCloud.Blazor.Auth;
using ArkCloud.Blazor.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// bUnit context wired with the *real* client-side auth stack (JwtAuthenticationStateProvider)
/// so tests exercise the same code path as production. The network call is replaced via
/// <see cref="FakeAuthHandler"/>, and token storage is replaced with <see cref="FakeTokenStorageService"/>
/// — bUnit's JSInterop, even in Loose mode, doesn't simulate a real persistent browser store
/// across separate JS invocations, so the real ProtectedSessionStorage-backed TokenStorageService
/// can't do a working get-after-set round trip in a test host (see FakeTokenStorageService's
/// remarks). Everything downstream of ITokenStorageService is still the real production code.
/// </summary>
public abstract class AuthTestContext : BunitContext
{
    protected FakeHttpMessageHandler FakeAuthHandler { get; } = new();

    protected AuthTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddScoped<ITokenStorageService, FakeTokenStorageService>();
        Services.AddScoped(_ => new AuthApiClient(
            new HttpClient(FakeAuthHandler) { BaseAddress = new Uri("https://fake-api.local/") }));
        Services.AddScoped<JwtAuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
        Services.AddAuthorizationCore();
        Services.AddCascadingAuthenticationState();
    }
}
