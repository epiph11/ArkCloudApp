using ArkCloud.Blazor.Services;

namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// In-memory stand-in for the real ProtectedSessionStorage-backed TokenStorageService.
///
/// bUnit's JSInterop, even in Loose mode, does not simulate an actual persistent browser store:
/// each JS invocation is intercepted independently and returns a default/canned response, with
/// no state carried between a "set" call and a later "get" call. TokenStorageService's real
/// implementation genuinely round-trips through JS interop (encrypt server-side, then
/// sessionStorage.setItem/getItem in the browser), so a get-after-set test — e.g. logging in,
/// then asserting the stored refresh token is used on logout — can never pass against the real
/// service in a bUnit test host, regardless of JSInterop configuration.
///
/// This fake keeps the same contract (ITokenStorageService) but backs it with a plain
/// dictionary, so tests that need a real round trip (see LogoutButtonTests) get one.
/// </summary>
public class FakeTokenStorageService : ITokenStorageService
{
    private const string AccessTokenKey = "accessToken";
    private const string RefreshTokenKey = "refreshToken";

    private readonly Dictionary<string, string> _store = new();

    public Task<string?> GetAccessTokenAsync() => Task.FromResult(TryGet(AccessTokenKey));

    public Task<string?> GetRefreshTokenAsync() => Task.FromResult(TryGet(RefreshTokenKey));

    public Task SetTokensAsync(string accessToken, string refreshToken)
    {
        _store[AccessTokenKey] = accessToken;
        _store[RefreshTokenKey] = refreshToken;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _store.Remove(AccessTokenKey);
        _store.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }

    private string? TryGet(string key) => _store.TryGetValue(key, out var value) ? value : null;
}
