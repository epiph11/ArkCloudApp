using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;

namespace ArkCloud.Blazor.Services;

/// <summary>
/// Typed client for /api/v1/products. Attaches "Authorization: Bearer &lt;token&gt;" itself
/// (via the injected, circuit-scoped TokenStorageService) right before every request — see
/// CustomersApiClient.AttachAuthorizationAsync for why this replaced a shared DelegatingHandler.
/// </summary>
public class ProductsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStorageService _tokenStorage;

    public ProductsApiClient(HttpClient httpClient, TokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    /// <summary>search matches name, SKU, or category (case-insensitive).</summary>
    public async Task<PagedResult<ProductResponse>> GetPagedAsync(
        string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();

        var query = $"api/v1/products?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return await _httpClient.GetFromJsonAsync<PagedResult<ProductResponse>>(query, cancellationToken)
               ?? new PagedResult<ProductResponse>();
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        return await _httpClient.GetFromJsonAsync<ProductResponse>($"api/v1/products/{id}", cancellationToken);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/products", request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to create the product.");
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken))!;
    }

    /// <summary>Requires the CanManageCatalog policy (Admin or Manager) server-side — plain Users get a 403.</summary>
    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/v1/products/{id}", request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to update the product.");
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken))!;
    }

    /// <summary>Requires the CanManageCatalog policy (Admin or Manager) server-side — plain Users get a 403.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.DeleteAsync($"api/v1/products/{id}", cancellationToken);
        await EnsureSuccessAsync(response, "Unable to delete the product.");
    }

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
