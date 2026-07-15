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

        var product = Product.Create("Widget", "WID-001", Money.Create(15, "EUR"), 10, "Gadgets");

        _customerRepository
            .Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        _productRepository
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _customerRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customer]);
        _productRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 3 }]
        };

        var result = await _sut.CreateAsync(request);

        result.TotalAmount.Should().Be(45);
        result.Status.Should().Be("Draft");
        result.CustomerName.Should().Be("John Doe");
        result.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.ProductName == "Widget" && i.Quantity == 3);
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
    public async Task GetByIdAsync_Should_Return_Enriched_Response_With_CustomerName_And_Items()
    {
        var customer = Customer.Create(
            "Jane", "Smith", Email.Create("jane@example.com"), Address.Create("St", "Lyon", "France"));
        var product = Product.Create("Widget", "WID-001", Money.Create(15, "EUR"), 10, "Gadgets");

        var order = Order.Create(customer.Id);
        order.AddItem(product.Id, 2, 15);

        _orderRepository
            .Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _customerRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customer]);
        _productRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var result = await _sut.GetByIdAsync(order.Id);

        result.CustomerName.Should().Be("Jane Smith");
        result.Items.Should().ContainSingle(i => i.Sku == "WID-001" && i.LineTotal == 30);
    }

    [Fact]
    public async Task GetPagedAsync_Should_Clamp_Page_And_PageSize_To_Valid_Ranges()
    {
        _orderRepository
            .Setup(x => x.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));
        _customerRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _productRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetPagedAsync(search: null, page: 0, pageSize: 500);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        _orderRepository.Verify(x => x.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
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
