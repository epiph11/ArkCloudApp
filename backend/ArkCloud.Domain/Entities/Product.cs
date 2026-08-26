using ArkCloud.Domain.Common;
using ArkCloud.Domain.Exceptions;
using ArkCloud.Domain.ValueObjects;

namespace ArkCloud.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; private set; }
    public string Sku { get; private set; }
    public Money UnitPrice { get; private set; }
    public int Stock { get; private set; }
    public string Category { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Product()
    {
        Name = default!;
        Sku = default!;
        UnitPrice = default!;
        Category = default!;
    }

    private Product(string name, string sku, Money unitPrice, int stock, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU is required.");
        if (stock < 0)
            throw new DomainException("Stock cannot be negative.");
        if (string.IsNullOrWhiteSpace(category))
            throw new DomainException("Category is required.");

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        UnitPrice = unitPrice;
        Stock = stock;
        Category = category.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public static Product Create(string name, string sku, Money unitPrice, int stock, string category)
        => new(name, sku, unitPrice, stock, category);

    public void Update(string name, string sku, Money unitPrice, int stock, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU is required.");
        if (stock < 0)
            throw new DomainException("Stock cannot be negative.");
        if (string.IsNullOrWhiteSpace(category))
            throw new DomainException("Category is required.");

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        UnitPrice = unitPrice;
        Stock = stock;
        Category = category.Trim();
    }
}
