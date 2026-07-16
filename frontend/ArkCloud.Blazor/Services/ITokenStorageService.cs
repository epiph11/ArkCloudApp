namespace ArkCloud.Blazor.Services;

/// <summary>
/// Abstraction over <see cref="TokenStorageService"/> so consumers (typed API clients,
/// JwtAuthenticationStateProvider) don't depend on the concrete ProtectedSessionStorage-backed
/// implementation directly. Production code registers <see cref="TokenStorageService"/>;
/// component tests register an in-memory fake instead (see
/// ArkCloud.Tests.Component/TestSupport/FakeTokenStorageService.cs) — bUnit's JSInterop in Loose
/// mode doesn't actually simulate a persistent browser store across separate JS interop calls, so
/// a real get-after-set round trip (as LogoutButtonTests needs) can't work against the real
/// ProtectedSessionStorage-backed service in a test host. This interface is the seam that makes
/// swapping that out possible without touching consumer code.
/// </summary>
public interface ITokenStorageService
{
    Task<string?> GetAccessTokenAsync();

    Task<string?> GetRefreshTokenAsync();

    Task SetTokensAsync(string accessToken, string refreshToken);

    Task ClearAsync();
}
