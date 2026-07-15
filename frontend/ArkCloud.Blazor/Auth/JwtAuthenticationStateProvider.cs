using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ArkCloud.Application.DTOs.Auth;
using ArkCloud.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace ArkCloud.Blazor.Auth;

/// <summary>
/// Reads the stored JWT and turns it into a ClaimsPrincipal for &lt;AuthorizeView&gt;/[Authorize].
/// This only parses the token locally to drive the UI — it never re-verifies the signature
/// client-side. The API independently and authoritatively validates the signature, issuer,
/// audience and expiry on every request via [Authorize], so a tampered/expired token is
/// rejected server-side regardless of what the client believes.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly TokenStorageService _tokenStorage;
    private readonly AuthApiClient _authApiClient;

    public JwtAuthenticationStateProvider(TokenStorageService tokenStorage, AuthApiClient authApiClient)
    {
        _tokenStorage = tokenStorage;
        _authApiClient = authApiClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new AuthenticationState(Anonymous);
        }

        return new AuthenticationState(BuildPrincipalFromToken(accessToken));
    }

    public async Task LoginAsync(LoginRequest request)
    {
        var response = await _authApiClient.LoginAsync(request);
        await PersistAndNotifyAsync(response);
    }

    public async Task RegisterAsync(RegisterRequest request)
    {
        var response = await _authApiClient.RegisterAsync(request);
        await PersistAndNotifyAsync(response);
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        await _tokenStorage.ClearAsync();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _authApiClient.LogoutAsync(refreshToken);
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    private async Task PersistAndNotifyAsync(AuthResponse response)
    {
        await _tokenStorage.SetTokensAsync(response.AccessToken, response.RefreshToken);

        var principal = BuildPrincipalFromToken(response.AccessToken);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    private static ClaimsPrincipal BuildPrincipalFromToken(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(accessToken);

        if (token.ValidTo < DateTime.UtcNow)
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity(
            token.Claims,
            authenticationType: "jwt",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
