using ArkCloud.Application.DTOs;
using ArkCloud.Application.Exceptions;
using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;

namespace ArkCloud.Application.Services;

public class OrderAppService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderAppService(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
            throw new NotFoundException("Customer not found.");

        var order = Order.Create(request.CustomerId);

        foreach (var item in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is null)
                throw new NotFoundException($"Product not found: {item.ProductId}");

            order.AddItem(product.Id, item.Quantity, product.UnitPrice.Amount);
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var enriched = await EnrichAsync([order], cancellationToken);
        return enriched[0];
    }

    public async Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException("Order not found.");

        var enriched = await EnrichAsync([order], cancellationToken);
        return enriched[0];
    }

    public async Task<List<OrderResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetAllAsync(cancellationToken);

        return await EnrichAsync(orders, cancellationToken);
    }

    /// <summary>
    /// search matches, in order of precedence: a CustomerId (GUID), an OrderStatus name
    /// ("Draft"/"Submitted"/"Paid"/"Cancelled"), or the owning customer's name/email.
    /// </summary>
    public async Task<PagedResult<OrderResponse>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (items, totalCount) = await _orderRepository.GetPagedAsync(search, page, pageSize, cancellationToken);

        return new PagedResult<OrderResponse>
        {
            Items = await EnrichAsync(items, cancellationToken),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException("Order not found.");

        order.Submit();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException("Order not found.");

        order.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Maps orders to responses with CustomerName/Items populated, using two batch lookups
    /// (all distinct customer IDs, all distinct product IDs across every item) instead of
    /// querying per-order/per-item.
    /// </summary>
    private async Task<List<OrderResponse>> EnrichAsync(List<Order> orders, CancellationToken cancellationToken)
    {
        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();

        var customers = await _customerRepository.GetByIdsAsync(customerIds, cancellationToken);
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        var customerLookup = customers.ToDictionary(c => c.Id);
        var productLookup = products.ToDictionary(p => p.Id);

        return orders.Select(order => new OrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = customerLookup.TryGetValue(order.CustomerId, out var customer)
                ? $"{customer.FirstName} {customer.LastName}"
                : "(unknown customer)",
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(item => new OrderItemResponse
            {
                ProductId = item.ProductId,
                ProductName = productLookup.TryGetValue(item.ProductId, out var product) ? product.Name : "(unknown product)",
                Sku = productLookup.TryGetValue(item.ProductId, out var skuProduct) ? skuProduct.Sku : string.Empty,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Bare mapping (no CustomerName/Items) — internal, not private: reused by
    /// DashboardAppService's "latest orders" widget, which only needs Status/TotalAmount and
    /// doesn't want the extra batch-lookup cost for a landing-page summary.
    /// </summary>
    internal static OrderResponse ToResponse(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Status = order.Status.ToString(),
        TotalAmount = order.TotalAmount
    };
}
