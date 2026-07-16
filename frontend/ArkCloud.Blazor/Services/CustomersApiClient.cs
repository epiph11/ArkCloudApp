using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;

namespace ArkCloud.Blazor.Services;

/// <summary>
/// Typed client for /api/v1/customers. Attaches "Authorization: Bearer &lt;token&gt;" itself
/// (via the injected, circuit-scoped TokenStorageService) right before every request — see
/// the comment on AttachAuthorizationAsync for why this replaced a shared DelegatingHandler.
/// </summary>
public class CustomersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;

    public CustomersApiClient(HttpClient httpClient, ITokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public async Task<PagedResult<CustomerResponse>> GetPagedAsync(
        string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();

        var query = $"api/v1/customers?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return await _httpClient.GetFromJsonAsync<PagedResult<CustomerResponse>>(query, cancellationToken)
               ?? new PagedResult<CustomerResponse>();
    }

    public async Task<CustomerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        return await _httpClient.GetFromJsonAsync<CustomerResponse>($"api/v1/customers/{id}", cancellationToken);
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/customers", request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to create the customer.");
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken))!;
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/v1/customers/{id}", request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to update the customer.");
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken))!;
    }

    /// <summary>Server-side gated to Admin — a plain User gets a 403, surfaced as an exception here.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.DeleteAsync($"api/v1/customers/{id}", cancellationToken);
        await EnsureSuccessAsync(response, "Unable to delete the customer.");
    }

    /// <summary>
    /// Sets (or clears) the Bearer header fresh on every call, read from the current circuit's
    /// TokenStorageService. This used to be a shared AuthorizationMessageHandler registered via
    /// .AddHttpMessageHandler&lt;T&gt;() in Program.cs — but IHttpClientFactory builds and pools
    /// that handler pipeline in its own internal scope (a "captive dependency": the pooled
    /// handler's lifetime, ~2 minutes by default, outlives and is separate from any one Blazor
    /// circuit). A Scoped TokenStorageService injected into that pooled handler ends up bound to
    /// whichever circuit happened to be active when the handler was first built, not the circuit
    /// actually making the current call — which is why requests kept going out with no token even
    /// though the UI correctly showed the user as logged in. This typed client itself IS resolved
    /// fresh per-circuit by AddHttpClient&lt;T&gt;, so reading the token here is safe.
    /// </summary>
    private async Task AttachAuthorizationAsync()
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new ApplicationException("You don't have permission to do that.");

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
