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
            Money.Create(request.UnitPrice, request.Currency),
            request.Stock,
            request.Category);

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(product);
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Product not found.");

        return ToResponse(product);
    }

    public async Task<List<ProductResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);

        return products.Select(ToResponse).ToList();
    }

    public async Task<PagedResult<ProductResponse>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (items, totalCount) = await _productRepository.GetPagedAsync(search, page, pageSize, cancellationToken);

        return new PagedResult<ProductResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Product not found.");

        product.Update(
            request.Name,
            request.Sku,
            Money.Create(request.UnitPrice, request.Currency),
            request.Stock,
            request.Category);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Product not found.");

        _productRepository.Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>internal, not private: reused by DashboardAppService's "latest products" widget.</summary>
    internal static ProductResponse ToResponse(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Sku = product.Sku,
        UnitPrice = product.UnitPrice.Amount,
        Currency = product.UnitPrice.Currency,
        Stock = product.Stock,
        Category = product.Category
    };
}
