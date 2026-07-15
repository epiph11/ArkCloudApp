using ArkCloud.Application.Interfaces;
using ArkCloud.Application.Services;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.Enums;
using ArkCloud.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace ArkCloud.Tests.Unit.Application;

public class DashboardAppServiceTests
{
    private readonly Mock<ICustomerRepository> _customerRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly DashboardAppService _sut;

    public DashboardAppServiceTests()
    {
        _sut = new DashboardAppService(_customerRepository.Object, _productRepository.Object, _orderRepository.Object);
    }

    [Fact]
    public async Task GetAsync_Should_Aggregate_Counts_And_Latest_Items()
    {
        var customer = Customer.Create(
            "John", "Doe", Email.Create("john@example.com"), Address.Create("St", "Paris", "France"));
        var product = Product.Create("Widget", "WID-001", Money.Create(15, "EUR"), 10, "Gadgets");
        var order = Order.Create(customer.Id);
        order.AddItem(product.Id, 2, 15);

        _customerRepository.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42);
        _productRepository.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7);
        _orderRepository.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10);

        _orderRepository.Setup(x => x.CountByStatusAsync(OrderStatus.Draft, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _orderRepository.Setup(x => x.CountByStatusAsync(OrderStatus.Submitted, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        _orderRepository.Setup(x => x.CountByStatusAsync(OrderStatus.Paid, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _orderRepository.Setup(x => x.CountByStatusAsync(OrderStatus.Cancelled, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _customerRepository.Setup(x => x.GetLatestAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync([customer]);
        _productRepository.Setup(x => x.GetLatestAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync([product]);
        _orderRepository.Setup(x => x.GetLatestAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync([order]);

        var result = await _sut.GetAsync();

        result.TotalCustomers.Should().Be(42);
        result.TotalProducts.Should().Be(7);
        result.TotalOrders.Should().Be(10);
        result.OrdersByStatus.Draft.Should().Be(3);
        result.OrdersByStatus.Submitted.Should().Be(4);
        result.OrdersByStatus.Paid.Should().Be(2);
        result.OrdersByStatus.Cancelled.Should().Be(1);

        result.LatestCustomers.Should().ContainSingle(c => c.Id == customer.Id);
        result.LatestProducts.Should().ContainSingle(p => p.Id == product.Id);
        result.LatestOrders.Should().ContainSingle(o => o.Id == order.Id && o.TotalAmount == 30);
    }
}
