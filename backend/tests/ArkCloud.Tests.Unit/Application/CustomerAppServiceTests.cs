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

public class CustomerAppServiceTests
{
    private readonly Mock<ICustomerRepository> _customerRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CustomerAppService _sut;

    public CustomerAppServiceTests()
    {
        _sut = new CustomerAppService(_customerRepository.Object, _orderRepository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_Customer_And_Return_Response()
    {
        var request = new CreateCustomerRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        };

        var result = await _sut.CreateAsync(request);

        result.FirstName.Should().Be("John");
        result.Email.Should().Be("john.doe@example.com");
        _customerRepository.Verify(x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Throw_NotFound_When_Customer_Missing()
    {
        _customerRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var action = async () => await _sut.GetByIdAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Changes_And_Return_Updated_Response()
    {
        var customer = Customer.Create(
            "John", "Doe", Email.Create("john@example.com"), Address.Create("St", "Paris", "France"));

        _customerRepository
            .Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var request = new UpdateCustomerRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Street = "Street 2",
            City = "Lyon",
            Country = "France"
        };

        var result = await _sut.UpdateAsync(customer.Id, request);

        result.FirstName.Should().Be("Jane");
        result.Email.Should().Be("jane.smith@example.com");
        result.City.Should().Be("Lyon");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_NotFound_When_Customer_Missing()
    {
        _customerRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var action = async () => await _sut.UpdateAsync(Guid.NewGuid(), new UpdateCustomerRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Street = "Street 2",
            City = "Lyon",
            Country = "France"
        });

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_Customer_When_No_Orders_Exist()
    {
        var customer = Customer.Create(
            "John", "Doe", Email.Create("john@example.com"), Address.Create("St", "Paris", "France"));

        _customerRepository
            .Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _orderRepository
            .Setup(x => x.ExistsForCustomerAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.DeleteAsync(customer.Id);

        _customerRepository.Verify(x => x.Remove(customer), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// GDPR fix (docs/rgpd-classification-donnees.md §3): a customer with existing orders must
    /// not be hard-deleted (it would leave orders.customer_id orphaned, and orders are retained
    /// under the legal-obligation exception) — it must be anonymized in place instead.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_Should_Anonymize_Instead_Of_Remove_When_Orders_Exist()
    {
        var customer = Customer.Create(
            "John", "Doe", Email.Create("john@example.com"), Address.Create("St", "Paris", "France"));

        _customerRepository
            .Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _orderRepository
            .Setup(x => x.ExistsForCustomerAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.DeleteAsync(customer.Id);

        _customerRepository.Verify(x => x.Remove(It.IsAny<Customer>()), Times.Never);
        customer.FirstName.Should().Be("Anonymized");
        customer.LastName.Should().Be("Anonymized");
        customer.Email.Value.Should().Contain("anonymized+");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_NotFound_When_Customer_Missing()
    {
        _customerRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var action = async () => await _sut.DeleteAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPagedAsync_Should_Clamp_Page_And_PageSize_To_Valid_Ranges()
    {
        _customerRepository
            .Setup(x => x.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Customer>(), 0));

        var result = await _sut.GetPagedAsync(search: null, page: 0, pageSize: 1000);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        _customerRepository.Verify(x => x.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
