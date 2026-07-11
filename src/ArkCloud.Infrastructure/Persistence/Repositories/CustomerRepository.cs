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

    public void Remove(Customer customer)
        => _context.Customers.Remove(customer);
}
