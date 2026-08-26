using ArkCloud.Domain.Entities;
using ArkCloud.Domain.Enums;

namespace ArkCloud.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Order>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>No free-text search field on Order yet; "search" filters by customer name/email via a join.</summary>
    Task<(List<Order> Items, int TotalCount)> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Count of orders in a given status, e.g. for a Dashboard "pending" widget.</summary>
    Task<int> CountByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);

    /// <summary>Most recently created orders first, for Dashboard widgets.</summary>
    Task<List<Order>> GetLatestAsync(int count, CancellationToken cancellationToken = default);
}
