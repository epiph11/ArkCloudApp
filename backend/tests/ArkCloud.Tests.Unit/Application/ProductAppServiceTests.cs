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

public class ProductAppServiceTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ProductAppService _sut;

    public ProductAppServiceTests()
    {
        _sut = new ProductAppService(_productRepository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_Product_And_Return_Response()
    {
        var request = new CreateProductRequest
        {
            Name = "Widget",
            Sku = "WID-001",
            UnitPrice = 15,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        };

        var result = await _sut.CreateAsync(request);

        result.Name.Should().Be("Widget");
        result.Sku.Should().Be("WID-001");
        result.Stock.Should().Be(10);
        result.Category.Should().Be("Gadgets");
        _productRepository.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Throw_NotFound_When_Product_Missing()
    {
        _productRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var action = async () => await _sut.GetByIdAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Changes_And_Return_Updated_Response()
    {
        var product = Product.Create("Widget", "WID-001", Money.Create(15, "EUR"), 10, "Gadgets");

        _productRepository
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var request = new UpdateProductRequest
        {
            Name = "Widget Pro",
            Sku = "WID-002",
            UnitPrice = 25,
            Currency = "USD",
            Stock = 3,
            Category = "Premium Gadgets"
        };

        var result = await _sut.UpdateAsync(product.Id, request);

        result.Name.Should().Be("Widget Pro");
        result.Sku.Should().Be("WID-002");
        result.UnitPrice.Should().Be(25);
        result.Currency.Should().Be("USD");
        result.Stock.Should().Be(3);
        result.Category.Should().Be("Premium Gadgets");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_NotFound_When_Product_Missing()
    {
        _productRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var action = async () => await _sut.UpdateAsync(Guid.NewGuid(), new UpdateProductRequest
        {
            Name = "Widget Pro",
            Sku = "WID-002",
            UnitPrice = 25,
            Currency = "USD",
            Stock = 3,
            Category = "Premium Gadgets"
        });

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_Product_When_Found()
    {
        var product = Product.Create("Widget", "WID-001", Money.Create(15, "EUR"), 10, "Gadgets");

        _productRepository
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _sut.DeleteAsync(product.Id);

        _productRepository.Verify(x => x.Remove(product), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_NotFound_When_Product_Missing()
    {
        _productRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var action = async () => await _sut.DeleteAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetPagedAsync_Should_Clamp_Page_And_PageSize_To_Valid_Ranges()
    {
        _productRepository
            .Setup(x => x.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product>(), 0));

        var result = await _sut.GetPagedAsync(search: null, page: -5, pageSize: 0);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        _productRepository.Verify(x => x.GetPagedAsync(null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
