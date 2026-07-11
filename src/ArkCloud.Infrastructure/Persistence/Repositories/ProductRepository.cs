using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkCloud.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ArkCloudDbContext _context;

    public ProductRepository(ArkCloudDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
        => await _context.Products.AddAsync(product, cancellationToken);

    public async Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Products.ToListAsync(cancellationToken);

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Remove(Product product)
        => _context.Products.Remove(product);
}
