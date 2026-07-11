using ArkCloud.Application.DTOs;
using ArkCloud.Application.Exceptions;
using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.ValueObjects;

namespace ArkCloud.Application.Services;

public class ProductAppService
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductAppService(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = Product.Create(
            request.Name,
            request.Sku,
            Money.Create(request.UnitPrice, request.Currency));

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            UnitPrice = product.UnitPrice.Amount,
            Currency = product.UnitPrice.Currency
        };
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Product not found.");

        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            UnitPrice = product.UnitPrice.Amount,
            Currency = product.UnitPrice.Currency
        };
    }

    public async Task<List<ProductResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);

        return products.Select(product => new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            UnitPrice = product.UnitPrice.Amount,
            Currency = product.UnitPrice.Currency
        }).ToList();
    }
}
