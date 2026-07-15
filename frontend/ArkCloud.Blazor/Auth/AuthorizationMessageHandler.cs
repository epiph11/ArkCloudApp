using System.Net.Http.Headers;
using ArkCloud.Blazor.Services;

namespace ArkCloud.Blazor.Auth;

/// <summary>
/// DEPRECATED / NO LONGER REGISTERED — kept only as a historical reference, do not re-wire this up.
///
/// This DelegatingHandler was registered via .AddHttpMessageHandler&lt;AuthorizationMessageHandler&gt;()
/// on each typed API client in Program.cs. That pattern is a classic ASP.NET Core "captive
/// dependency" bug: IHttpClientFactory builds and pools the message-handler pipeline in its own
/// internal scope (handler lifetime defaults to ~2 minutes), which is separate from the Blazor
/// Server circuit making any given call. The Scoped TokenStorageService injected below therefore
/// got resolved once against whichever scope happened to construct the pooled handler, not the
/// circuit of the user actually calling the API — so requests kept going out with no (or a stale)
/// Authorization header even when the UI correctly showed the user as logged in.
///
/// Fixed by moving header-attachment into each typed client itself (CustomersApiClient,
/// ProductsApiClient, OrdersApiClient, DashboardApiClient) — those ARE resolved fresh per-circuit
/// by AddHttpClient&lt;T&gt;, so reading TokenStorageService there is safe. See
/// CustomersApiClient.AttachAuthorizationAsync.
/// </summary>
public class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly TokenStorageService _tokenStorage;

    public AuthorizationMessageHandler(TokenStorageService tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync();

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
