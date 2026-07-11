using ArkCloud.Application.DTOs;
using ArkCloud.Application.Exceptions;
using ArkCloud.Application.Interfaces;
using ArkCloud.Application.Services;
using ArkCloud.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ArkCloud.Tests.Unit.Application;

public class CustomerAppServiceTests
{
    private readonly Mock<ICustomerRepository> _customerRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CustomerAppService _sut;

    public CustomerAppServiceTests()
    {
        _sut = new CustomerAppService(_customerRepository.Object, _unitOfWork.Object);
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
}
