using ArkCloud.Blazor.Auth;
using ArkCloud.Blazor.Components;
using ArkCloud.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

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

// AuthApiClient talks to /auth/* (login/register/refresh/logout) — no token to attach yet.
builder.Services.AddHttpClient<AuthApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Every other typed client attaches "Authorization: Bearer <token>" itself, reading it fresh
// from its own injected (circuit-scoped) TokenStorageService — see
// CustomersApiClient.AttachAuthorizationAsync for why this is NOT done via a shared
// .AddHttpMessageHandler<T>() DelegatingHandler (captive-dependency bug: IHttpClientFactory
// pools that handler pipeline in its own internal scope, separate from the Blazor circuit
// making the call, so a Scoped TokenStorageService injected into it doesn't track the current
// user's session).
builder.Services.AddHttpClient<CustomersApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddHttpClient<ProductsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddHttpClient<OrdersApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddHttpClient<DashboardApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

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
