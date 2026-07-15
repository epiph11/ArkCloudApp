using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;

namespace ArkCloud.Blazor.Services;

/// <summary>
/// Typed client for GET /api/v1/dashboard. Attaches "Authorization: Bearer &lt;token&gt;" itself
/// (via the injected, circuit-scoped TokenStorageService) right before every request — see
/// CustomersApiClient.AttachAuthorizationAsync for why this replaced a shared DelegatingHandler.
/// </summary>
public class DashboardApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStorageService _tokenStorage;

    public DashboardApiClient(HttpClient httpClient, TokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        return await _httpClient.GetFromJsonAsync<DashboardResponse>("api/v1/dashboard", cancellationToken)
               ?? new DashboardResponse();
    }

    private async Task AttachAuthorizationAsync()
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
