using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ArkCloud.Blazor.Services;

/// <summary>
/// Wraps ProtectedSessionStorage (Blazor Server's browser-session-scoped, encrypted
/// storage — see Step 14). Never uses localStorage or a static/in-memory field, both of
/// which would leak the token across users/tabs or survive longer than intended.
/// </summary>
public class TokenStorageService
{
    private const string AccessTokenKey = "arkcloud.accessToken";
    private const string RefreshTokenKey = "arkcloud.refreshToken";

    private readonly ProtectedSessionStorage _sessionStorage;

    public TokenStorageService(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    public Task<string?> GetAccessTokenAsync() => TryGetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() => TryGetAsync(RefreshTokenKey);

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await _sessionStorage.SetAsync(AccessTokenKey, accessToken);
        await _sessionStorage.SetAsync(RefreshTokenKey, refreshToken);
    }

    public async Task ClearAsync()
    {
        await _sessionStorage.DeleteAsync(AccessTokenKey);
        await _sessionStorage.DeleteAsync(RefreshTokenKey);
    }

    private async Task<string?> TryGetAsync(string key)
    {
        try
        {
            var result = await _sessionStorage.GetAsync<string>(key);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            // JS interop isn't available yet during static/prerendering — treat as anonymous
            // rather than throwing, so the first render of a page doesn't crash.
            return null;
        }
    }
}
