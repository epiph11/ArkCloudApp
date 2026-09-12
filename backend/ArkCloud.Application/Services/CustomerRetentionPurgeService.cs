using ArkCloud.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ArkCloud.Application.Services;

/// <summary>
/// RGPD automated retention purge (docs/rgpd-classification-donnees.md §2/§5). Decision made
/// with the user (Sprint 6, 12/09/2026): a customer is eligible once <see cref="RetentionYears"/>
/// years have passed since their last order (or since account creation, if they have none), and
/// an eligible customer is anonymized — never hard-deleted — using the same
/// <c>Customer.Anonymize()</c> already used by the manual erasure path
/// (<see cref="CustomerAppService.DeleteAsync"/>), whether or not they have orders. This keeps a
/// single anonymization behavior across both the on-demand RGPD erasure request and this
/// scheduled sweep, rather than two subtly different code paths doing the same thing.
///
/// Deliberately framework-agnostic (no scheduling, no hosting concerns here) so it can be driven
/// by whatever mechanism each cloud actually supports — see ADR-0012 for why that mechanism
/// differs between Azure (an in-process hosted service in ArkCloud.API, since only the API's App
/// Service already has network access to Postgres — same constraint that produced ADR-0010) and
/// AWS (folded into the existing `secret-rotation` Lambda, which already runs inside the VPC).
/// </summary>
public class CustomerRetentionPurgeService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CustomerRetentionPurgeService> _logger;

    /// <summary>
    /// Years of inactivity after which a customer becomes eligible for anonymization. 3 years —
    /// decided with the user, not invented here; see docs/rgpd-classification-donnees.md §2 and
    /// ADR-0012. Kept as a settable property (not a constructor-only value) so a caller can pass
    /// it in from configuration without a dedicated options type in this layer — Application
    /// stays free of any framework-specific configuration abstraction, consistent with the rest
    /// of this project's Clean Architecture fitness functions.
    /// </summary>
    public int RetentionYears { get; init; } = 3;

    public CustomerRetentionPurgeService(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork,
        ILogger<CustomerRetentionPurgeService> logger)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Anonymizes every customer eligible under the retention policy. Safe to call repeatedly
    /// (daily, from a hosted service) — already-anonymized customers are excluded by the
    /// repository query, so a re-run only ever processes newly-eligible rows. Returns the number
    /// of customers anonymized, for the caller to log/report.
    /// </summary>
    public async Task<int> PurgeInactiveCustomersAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddYears(-RetentionYears);

        var eligible = await _customerRepository.GetEligibleForRetentionPurgeAsync(cutoffDate, cancellationToken);

        if (eligible.Count == 0)
        {
            _logger.LogInformation(
                "RGPD retention purge: no customer inactive since before {CutoffDate:yyyy-MM-dd}.", cutoffDate);
            return 0;
        }

        foreach (var customer in eligible)
        {
            customer.Anonymize();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Deliberately not logging which customers (no Id/email) — the point of this feature is
        // minimizing personal data, logging identifiers of the very records just anonymized would
        // undercut it. A count is enough for operational visibility.
        _logger.LogInformation(
            "RGPD retention purge: anonymized {Count} customer(s) inactive since before {CutoffDate:yyyy-MM-dd}.",
            eligible.Count, cutoffDate);

        return eligible.Count;
    }
}
