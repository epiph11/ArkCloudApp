using System.Text;
using System.Threading.RateLimiting;
using Azure.Identity;
using ArkCloud.API.Authorization;
using ArkCloud.API.HostedServices;
using ArkCloud.API.Middlewares;
using ArkCloud.Application.Interfaces;
using ArkCloud.Application.Services;
using ArkCloud.Application.Validators;
using ArkCloud.Domain.Common;
using ArkCloud.Infrastructure.Authentication;
using ArkCloud.Infrastructure.DependencyInjection;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

// Reads APPLICATIONINSIGHTS_CONNECTION_STRING from configuration/environment automatically —
// set by ArkCloudInfra's app-service module as an App Service app setting. Required explicitly
// because this runs as a custom Docker image: Azure's codeless auto-instrumentation only
// applies to its own built-in runtime stacks, not arbitrary containers.
builder.Services.AddApplicationInsightsTelemetry();

// ---------------------------------------------------------------------------
// Secret management
// ---------------------------------------------------------------------------
// Dev:  `dotnet user-secrets set "Jwt:Key" "..."` (see UserSecretsId in the .csproj) — never
//       committed, lives outside the repo in %APPDATA%/Microsoft/UserSecrets.
// Prod: this app never reads a raw JWT key / DB password / API key from appsettings.json —
//       those are placeholders ("") in source control. Instead, when KeyVault:Uri is set
//       (e.g. via an App Service application setting, not a committed file), the standard
//       Azure Key Vault configuration provider layers secrets on top of appsettings.json,
//       keyed by name with "--" standing in for ":" (so a Key Vault secret named
//       "Jwt--Key" becomes configuration key "Jwt:Key", "ConnectionStrings--DefaultConnection"
//       becomes the connection string, etc.) — no code change needed when a secret rotates.
// DefaultAzureCredential resolves to the App Service/VM's managed identity in Azure, or to
// `az login` / environment credentials locally — no secret or connection string for Key
// Vault itself is ever configured, which is the point.
// This is pattern-only for now: no Key Vault instance exists yet, so KeyVault:Uri is unset
// and this block is a no-op — appsettings/user-secrets/environment variables behave exactly
// as before. Wiring it up for real is just: create the vault, grant the App Service's managed
// identity "Key Vault Secrets User", and set KeyVault:Uri.
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ArkCloud API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT access token (no 'Bearer ' prefix needed)."
    });

    // Swashbuckle.AspNetCore v10+ (Microsoft.OpenApi v2) replaced the old
    // `new OpenApiSecurityScheme { Reference = ... }` dictionary-key pattern with a
    // document-scoped OpenApiSecuritySchemeReference, resolved via a delegate.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// FluentValidation.AspNetCore (automatic MVC validation) is deprecated and was removed.
// Validators are registered here via FluentValidation.DependencyInjectionExtensions and
// invoked explicitly in each controller action with IValidator<T>.ValidateAndThrowAsync(),
// which throws FluentValidation.ValidationException — caught by ExceptionHandlingMiddleware
// and turned into a 400 application/problem+json response.
// Both CreateCustomerRequestValidator and RegisterRequestValidator live in ArkCloud.Application,
// so this single call also picks up RegisterRequestValidator/LoginRequestValidator.
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<CustomerAppService>();
builder.Services.AddScoped<OrderAppService>();
builder.Services.AddScoped<ProductAppService>();
builder.Services.AddScoped<DashboardAppService>();
builder.Services.AddScoped<AuthService>();

// ---------------------------------------------------------------------------
// RGPD retention purge (docs/rgpd-classification-donnees.md §2/§5, ADR-0012).
// ---------------------------------------------------------------------------
// RetentionYears defaults to 3 (the threshold decided with the user) so the service still has a
// sane value even where this section is never configured (e.g. AWS, where the equivalent job is
// the secret-rotation Lambda instead — see the hosted service's own doc comment below).
var gdprRetentionYears = builder.Configuration.GetValue("Gdpr:CustomerRetentionYears", 3);
builder.Services.AddScoped(sp => new CustomerRetentionPurgeService(
    sp.GetRequiredService<ICustomerRepository>(),
    sp.GetRequiredService<IUnitOfWork>(),
    sp.GetRequiredService<ILogger<CustomerRetentionPurgeService>>())
{
    RetentionYears = gdprRetentionYears
});

// Opt-in, not automatic: only the Azure App Service (via an app setting Terraform sets on the
// api app_service module) should ever run this in-process. On AWS the same job runs as a Lambda
// inside the VPC instead (folded into modules/aws/secret-rotation) — see ADR-0012 for why the
// mechanism has to differ between the two clouds. Defaults to false so a container that forgets
// to set this explicitly does nothing, rather than silently starting a background sweep.
if (builder.Configuration.GetValue("Gdpr:RunRetentionPurgeInProcess", false))
{
    builder.Services.AddHostedService<CustomerRetentionPurgeHostedService>();
}

// ---------------------------------------------------------------------------
// Authentication (JWT bearer)
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtKey = jwtSection["Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured. In development, run " +
        "'dotnet user-secrets set \"Jwt:Key\" \"<random 64+ char value>\" --project backend/ArkCloud.API' " +
        "from the repo root. " +
        "In production, source it from Azure Key Vault / AWS Secrets Manager — never commit it.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Log.Warning("Unauthorized request to {Path}.", context.Request.Path);
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                Log.Warning("Forbidden request to {Path}.", context.Request.Path);
                return Task.CompletedTask;
            }
        };
    });

// ---------------------------------------------------------------------------
// Authorization policies
// ---------------------------------------------------------------------------
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PolicyNames.AdminOnly, policy => policy.RequireRole(RoleNames.Admin))
    .AddPolicy(PolicyNames.ManagersOnly, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Manager))
    .AddPolicy(PolicyNames.CanCreateOrders, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Manager, RoleNames.User))
    .AddPolicy(PolicyNames.CanManageCatalog, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Manager));

// ---------------------------------------------------------------------------
// CORS — only the Blazor frontend origin(s) are allowed.
// ---------------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// Rate limiting — protects /auth/* against brute-force / credential stuffing.
// Permit count is configurable so a strict prod default (5/min) doesn't get in the
// way of local dev / integration tests, which — running through TestServer with no
// real socket — all share the same "unknown" IP partition.
// ---------------------------------------------------------------------------
var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
var loginWindowSeconds = builder.Configuration.GetValue("RateLimiting:LoginWindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromSeconds(loginWindowSeconds),
                QueueLimit = 0
            }));
});

// ---------------------------------------------------------------------------
// Request size limits — mitigates oversized payloads / JSON-bomb style abuse.
// ---------------------------------------------------------------------------
const long maxRequestBodyBytes = 5 * 1024 * 1024; // 5 MB

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxRequestBodyBytes;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
});

var app = builder.Build();

// AWS ALB path-based routing (no custom domain yet, see modules/aws/alb) forwards the full
// incoming path unchanged — a public request for "/api/customers" arrives here as literally
// "/api/customers", but every route in this app is registered without that prefix (e.g.
// "/customers"). UsePathBase strips it into PathBase before routing runs. It's a no-op for
// any request that doesn't start with "/api" — Azure App Service never sends that prefix
// (each app has its own hostname there, no path-based routing), and the ALB's own internal
// health check hits the container directly at "/health" (bypassing listener rules entirely),
// so neither is affected by this.
app.UsePathBase("/api");

// Load-balancer health check (ALB target group / Azure App Service health check both probe
// this path — see modules/aws/alb and modules/azure/app-service's health_check_path, default
// "/health" in both). Mapped before auth/rate-limiting middleware and with no [Authorize] so
// it's always reachable anonymously, matching what a health probe needs.
app.MapGet("/health", () => Results.Ok());

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("BlazorFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();

public partial class Program { }
