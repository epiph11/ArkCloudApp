using ArkCloud.Application.DTOs;
using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Enums;

namespace ArkCloud.Application.Services;

/// <summary>
/// Aggregates counts and "latest N" widgets from the three catalog/order repositories for the
/// Blazor Dashboard landing page. Read-only — no writes, no validation needed.
/// </summary>
public class DashboardAppService
{
    private const int LatestItemsCount = 5;

    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public DashboardAppService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var totalCustomers = await _customerRepository.CountAsync(cancellationToken);
        var totalProducts = await _productRepository.CountAsync(cancellationToken);
        var totalOrders = await _orderRepository.CountAsync(cancellationToken);

        var draftCount = await _orderRepository.CountByStatusAsync(OrderStatus.Draft, cancellationToken);
        var submittedCount = await _orderRepository.CountByStatusAsync(OrderStatus.Submitted, cancellationToken);
        var paidCount = await _orderRepository.CountByStatusAsync(OrderStatus.Paid, cancellationToken);
        var cancelledCount = await _orderRepository.CountByStatusAsync(OrderStatus.Cancelled, cancellationToken);

        var latestCustomers = await _customerRepository.GetLatestAsync(LatestItemsCount, cancellationToken);
        var latestProducts = await _productRepository.GetLatestAsync(LatestItemsCount, cancellationToken);
        var latestOrders = await _orderRepository.GetLatestAsync(LatestItemsCount, cancellationToken);

        return new DashboardResponse
        {
            TotalCustomers = totalCustomers,
            TotalProducts = totalProducts,
            TotalOrders = totalOrders,
            OrdersByStatus = new OrderStatusCounts
            {
                Draft = draftCount,
                Submitted = submittedCount,
                Paid = paidCount,
                Cancelled = cancelledCount
            },
            LatestCustomers = latestCustomers.Select(CustomerAppService.ToResponse).ToList(),
            LatestProducts = latestProducts.Select(ProductAppService.ToResponse).ToList(),
            LatestOrders = latestOrders.Select(OrderAppService.ToResponse).ToList()
        };
    }
}
