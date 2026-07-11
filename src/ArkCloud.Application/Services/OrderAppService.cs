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

        return new OrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount
        };
    }

    public async Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException("Order not found.");

        return new OrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount
        };
    }

    public async Task<List<OrderResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetAllAsync(cancellationToken);

        return orders.Select(order => new OrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount
        }).ToList();
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
}
