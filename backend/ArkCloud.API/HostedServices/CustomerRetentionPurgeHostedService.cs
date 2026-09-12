using ArkCloud.Application.Services;

namespace ArkCloud.API.HostedServices;

/// <summary>
/// Drives <see cref="CustomerRetentionPurgeService"/> on a daily timer. Exists in ArkCloud.API,
/// not ArkCloud.Application, deliberately: hosting/scheduling is an API-layer concern (the
/// Application service itself stays framework-agnostic, see its own doc comment), and this is
/// the Azure-side half of ADR-0012's asymmetric mechanism — this hosted service only ever runs
/// where <c>Gdpr:RunRetentionPurgeInProcess</c> is explicitly enabled (see Program.cs), which
/// Terraform sets to true only on the Azure App Service (app_service module), never on the AWS
/// ECS task definition, where the equivalent job is a Lambda instead (folded into
/// modules/aws/secret-rotation, which already runs inside the VPC). Running both at once would
/// be harmless (the underlying query is idempotent) but wasteful and confusing to reason about,
/// hence the explicit opt-in rather than "runs everywhere by default".
///
/// Runs once at startup (after a short delay, to let the host finish coming up) and then every
/// 24h — the eligibility window is measured in years, so daily polling is far more often than
/// strictly needed, but it's cheap (a single indexed-ish query, see
/// CustomerRepository.GetEligibleForRetentionPurgeAsync) and keeps this simple: no cron
/// expression to parse, no missed-run recovery logic to write.
/// </summary>
public class CustomerRetentionPurgeHostedService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CustomerRetentionPurgeHostedService> _logger;

    public CustomerRetentionPurgeHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<CustomerRetentionPurgeHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await TryWaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        // CustomerRetentionPurgeService and its dependencies (ICustomerRepository, IUnitOfWork,
        // which wrap a scoped DbContext) are registered as Scoped — a BackgroundService is a
        // singleton, so a scope has to be created explicitly for each run rather than injecting
        // them directly into this class's constructor.
        using var scope = _scopeFactory.CreateScope();
        var purgeService = scope.ServiceProvider.GetRequiredService<CustomerRetentionPurgeService>();

        try
        {
            await purgeService.PurgeInactiveCustomersAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never let a purge failure crash the host — this is a background compliance sweep,
            // not a request in flight. Logged and retried on the next tick.
            _logger.LogError(ex, "RGPD retention purge run failed; will retry on the next scheduled tick.");
        }
    }

    private static async Task<bool> TryWaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
