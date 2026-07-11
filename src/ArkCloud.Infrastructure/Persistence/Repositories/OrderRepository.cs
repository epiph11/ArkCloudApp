using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
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
        => await _context.Orders.Include("_items").ToListAsync(cancellationToken);

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Orders.Include("_items").FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}
