using ArkCloud.Application.DTOs;
using ArkCloud.Application.Exceptions;
using ArkCloud.Application.Interfaces;
using ArkCloud.Application.Services;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace ArkCloud.Tests.Unit.Application;

public class OrderAppServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ICustomerRepository> _customerRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OrderAppService _sut;

    public OrderAppServiceTests()
    {
        _sut = new OrderAppService(
            _orderRepository.Object,
            _customerRepository.Object,
            _productRepository.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_NotFound_When_Customer_Missing()
    {
        _customerRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }]
        };

        var action = async () => await _sut.CreateAsync(request);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_NotFound_When_Product_Missing()
    {
        var customer = Customer.Create(
            "John", "Doe", Email.Create("john@example.com"), Address.Create("St", "Paris", "France"));

        _customerRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        _productRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }]
        };

        var action = async () => await _sut.CreateAsync(request);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_Should_Create_Order_With_Total_From_Product_Price()
    {
        var customer = Customer.Create(
            "John", "Doe", Email.Create("john@example.com"), Address.Create("St", "Paris", "France"));

        var product = Product.Create("Widget", "WID-001", Money.Create(15, "EUR"));

        _customerRepository
            .Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        _productRepository
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 3 }]
        };

        var result = await _sut.CreateAsync(request);

        result.TotalAmount.Should().Be(45);
        result.Status.Should().Be("Draft");
        _orderRepository.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Throw_NotFound_When_Order_Missing()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var action = async () => await _sut.GetByIdAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SubmitAsync_Should_Persist_Submitted_Status()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 1, 10);

        _orderRepository
            .Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await _sut.SubmitAsync(order.Id);

        order.Status.ToString().Should().Be("Submitted");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
