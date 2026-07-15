using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArkCloud.Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly ArkCloudDbContext _context;

    public OrderRepository(ArkCloudDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
        => await _context.Orders.AddAsync(order, cancellationToken);

    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Orders.Include(x => x.Items).ToListAsync(cancellationToken);

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<(List<Order> Items, int TotalCount)> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.Include(x => x.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            if (Guid.TryParse(term, out var customerId))
            {
                query = query.Where(x => x.CustomerId == customerId);
            }
            else if (Enum.TryParse<OrderStatus>(term, ignoreCase: true, out var status))
            {
                query = query.Where(x => x.Status == status);
            }
            else
            {
                // No free-text order field to match on directly — fall back to matching the
                // owning customer's name/email so "search" still behaves as users expect.
                var matchingCustomerIds = await _context.Customers
                    .Where(c =>
                        EF.Functions.ILike(c.FirstName, $"%{term}%") ||
                        EF.Functions.ILike(c.LastName, $"%{term}%") ||
                        EF.Functions.ILike(c.Email.Value, $"%{term}%"))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);

                query = query.Where(x => matchingCustomerIds.Contains(x.CustomerId));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.Orders.CountAsync(cancellationToken);

    public async Task<int> CountByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default)
        => await _context.Orders.CountAsync(x => x.Status == status, cancellationToken);

    public async Task<List<Order>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
        => await _context.Orders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
}
