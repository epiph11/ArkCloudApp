using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;

namespace ArkCloud.Blazor.Services;

/// <summary>
/// Typed client for /api/v1/orders. Attaches "Authorization: Bearer &lt;token&gt;" itself
/// (via the injected, circuit-scoped TokenStorageService) right before every request — see
/// CustomersApiClient.AttachAuthorizationAsync for why this replaced a shared DelegatingHandler.
/// </summary>
public class OrdersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;

    public OrdersApiClient(HttpClient httpClient, ITokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    /// <summary>search matches a CustomerId, an order status name ("Draft"/"Submitted"/"Paid"/"Cancelled"), or the customer's name/email.</summary>
    public async Task<PagedResult<OrderResponse>> GetPagedAsync(
        string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();

        var query = $"api/v1/orders?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return await _httpClient.GetFromJsonAsync<PagedResult<OrderResponse>>(query, cancellationToken)
               ?? new PagedResult<OrderResponse>();
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        return await _httpClient.GetFromJsonAsync<OrderResponse>($"api/v1/orders/{id}", cancellationToken);
    }

    /// <summary>Creates a draft order. Requires the CanCreateOrders policy server-side.</summary>
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/orders", request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to create the order.");
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken))!;
    }

    /// <summary>Moves a draft order to Submitted. Requires the CanCreateOrders policy server-side.</summary>
    public async Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.PostAsync($"api/v1/orders/{id}/submit", content: null, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to submit the order.");
    }

    /// <summary>Cancels an order. Requires the ManagersOnly policy server-side — a plain User gets a 403.</summary>
    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachAuthorizationAsync();
        var response = await _httpClient.PostAsync($"api/v1/orders/{id}/cancel", content: null, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to cancel the order.");
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
