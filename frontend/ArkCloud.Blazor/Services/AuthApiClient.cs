using System.Net.Http.Json;
using ArkCloud.Application.DTOs.Auth;

namespace ArkCloud.Blazor.Services;

/// <summary>Typed client for POST /api/v1/auth/*. Deliberately has no Bearer token attached (see Step 15).</summary>
public class AuthApiClient
{
    private readonly HttpClient _httpClient;

    public AuthApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request, cancellationToken);
        await EnsureSuccessAsync(response, "Invalid email or password.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken))!;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/register", request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to create the account.");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken))!;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Best-effort: the client always clears its local tokens regardless of whether
        // the server call to revoke the refresh token succeeds.
        try
        {
            await _httpClient.PostAsJsonAsync(
                "api/v1/auth/logout",
                new RefreshTokenRequest { RefreshToken = refreshToken },
                cancellationToken);
        }
        catch (HttpRequestException)
        {
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage)
    {
        if (response.IsSuccessStatusCode)
            return;

        ProblemPayload? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        }
        catch
        {
            // Response body wasn't the expected problem+json shape — fall back below.
        }

        throw new ApplicationException(problem?.Detail ?? fallbackMessage);
    }

    private class ProblemPayload
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }
}
