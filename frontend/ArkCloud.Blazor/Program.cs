using ArkCloud.Blazor.Auth;
using ArkCloud.Blazor.Components;
using ArkCloud.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Reads APPLICATIONINSIGHTS_CONNECTION_STRING from configuration/environment automatically —
// set by ArkCloudInfra's app-service module as an App Service app setting. Required explicitly
// because this runs as a custom Docker image: Azure's codeless auto-instrumentation only
// applies to its own built-in runtime stacks, not arbitrary containers.
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Server-side, per-circuit token storage backed by the browser's protected session
// storage (encrypted client-side, never touches localStorage — see Step 14).
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<ITokenStorageService, TokenStorageService>();

// A single JwtAuthenticationStateProvider instance is exposed both as the concrete
// type (so Login/Register/Logout components can call its auth methods) and as the
// framework-facing AuthenticationStateProvider (so <AuthorizeView>/[Authorize] work).
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured in appsettings.json.");

// AWS only (Sprint 6): the ALB's HTTPS listener presents a self-signed certificate — no real
// domain exists yet to get a trusted ACM cert validated against (see ArkCloudInfra's
// modules/aws/alb header comment). Default TLS validation would reject every API call outright
// the moment Api__BaseUrl becomes https://. ArkCloudInfra's AWS ECS wiring sets
// Api__TrustSelfSignedCert=true alongside the https:// BaseUrl; Azure never sets it (its cert is
// a real Microsoft-issued one on azurewebsites.net), so this is inert there. Scoped as tightly
// as a self-signed workaround gets without real cert pinning: skips chain validation only for
// requests to this exact configured API host, never a blanket bypass. Delete this the moment a
// real domain + ACM DNS-validated cert exists for the AWS ALB.
var trustSelfSignedApiCert = builder.Configuration.GetValue<bool>("Api:TrustSelfSignedCert");
var apiHost = new Uri(apiBaseUrl).Host;

void ConfigureApiClientCertTrust(IHttpClientBuilder httpClientBuilder)
{
    if (!trustSelfSignedApiCert)
    {
        return;
    }

    httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (request, _, _, _) =>
            request.RequestUri?.Host == apiHost,
    });
}

// AuthApiClient talks to /auth/* (login/register/refresh/logout) — no token to attach yet.
ConfigureApiClientCertTrust(builder.Services.AddHttpClient<AuthApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}));

// Every other typed client attaches "Authorization: Bearer <token>" itself, reading it fresh
// from its own injected (circuit-scoped) TokenStorageService — see
// CustomersApiClient.AttachAuthorizationAsync for why this is NOT done via a shared
// .AddHttpMessageHandler<T>() DelegatingHandler (captive-dependency bug: IHttpClientFactory
// pools that handler pipeline in its own internal scope, separate from the Blazor circuit
// making the call, so a Scoped TokenStorageService injected into it doesn't track the current
// user's session).
ConfigureApiClientCertTrust(builder.Services.AddHttpClient<CustomersApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}));

ConfigureApiClientCertTrust(builder.Services.AddHttpClient<ProductsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}));

ConfigureApiClientCertTrust(builder.Services.AddHttpClient<OrdersApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}));

ConfigureApiClientCertTrust(builder.Services.AddHttpClient<DashboardApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}));

var app = builder.Build();

// Load-balancer health check — same convention as ArkCloud.API, see comment there.
app.MapGet("/health", () => Results.Ok());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
