using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkCloud.Infrastructure.Persistence;

public class ArkCloudDbContext : DbContext, IUnitOfWork
{
    public ArkCloudDbContext(DbContextOptions<ArkCloudDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArkCloudDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
