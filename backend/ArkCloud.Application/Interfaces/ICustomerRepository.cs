using ArkCloud.Domain.Entities;

namespace ArkCloud.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Customer>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive match against first name, last name, and email.</summary>
    Task<(List<Customer> Items, int TotalCount)> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    void Remove(Customer customer);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Most recently created customers first, for Dashboard widgets.</summary>
    Task<List<Customer>> GetLatestAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup, e.g. to enrich a page of Orders with customer names in one round trip.</summary>
    Task<List<Customer>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Customers eligible for the RGPD automated retention purge (docs/rgpd-classification-donnees.md
    /// §2/§5 — threshold decided with the user: <paramref name="cutoffDate"/> years of inactivity).
    /// A customer is eligible when their most recent order predates the cutoff, or — if they have
    /// no order at all — when the customer record itself predates the cutoff. Already-anonymized
    /// customers (see Customer.Anonymize()) are excluded so a scheduled job calling this
    /// repeatedly stays idempotent instead of reprocessing the same rows forever.
    /// </summary>
    Task<List<Customer>> GetEligibleForRetentionPurgeAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
