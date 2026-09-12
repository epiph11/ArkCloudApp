using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkCloud.Infrastructure.Persistence.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly ArkCloudDbContext _context;

    public CustomerRepository(ArkCloudDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
        => await _context.Customers.AddAsync(customer, cancellationToken);

    public async Task<List<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Customers.ToListAsync(cancellationToken);

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<(List<Customer> Items, int TotalCount)> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, term) ||
                EF.Functions.ILike(x.LastName, term) ||
                EF.Functions.ILike(x.Email.Value, term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Remove(Customer customer)
        => _context.Customers.Remove(customer);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.Customers.CountAsync(cancellationToken);

    public async Task<List<Customer>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
        => await _context.Customers
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<List<Customer>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _context.Customers.Where(x => idList.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<List<Customer>> GetEligibleForRetentionPurgeAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        // Idempotency marker: Customer.Anonymize() always writes this exact email domain, and
        // nothing else in the app does — a real customer email can never collide with it (Email
        // itself validates a plausible address, but this domain is reserved/invalid by design).
        // Cheaper and less ambiguous than checking FirstName == "Anonymized", which a real
        // (if unlikely) customer name could theoretically match.
        const string anonymizedEmailSuffix = "@arkcloud.invalid";

        var query =
            from c in _context.Customers
            where !c.Email.Value.EndsWith(anonymizedEmailSuffix)
            let lastOrderDate = _context.Orders
                .Where(o => o.CustomerId == c.Id)
                .Select(o => (DateTime?)o.CreatedAt)
                .Max()
            where (lastOrderDate == null && c.CreatedAt < cutoffDate)
               || (lastOrderDate != null && lastOrderDate < cutoffDate)
            select c;

        return await query.ToListAsync(cancellationToken);
    }
}
