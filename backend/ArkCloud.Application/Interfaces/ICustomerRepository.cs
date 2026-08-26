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
}
