using ArkCloud.Domain.Entities;

namespace ArkCloud.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive match against name, SKU, and category.</summary>
    Task<(List<Product> Items, int TotalCount)> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    void Remove(Product product);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Most recently created products first, for Dashboard widgets.</summary>
    Task<List<Product>> GetLatestAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup, e.g. to enrich a page of Orders with product names in one round trip.</summary>
    Task<List<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
