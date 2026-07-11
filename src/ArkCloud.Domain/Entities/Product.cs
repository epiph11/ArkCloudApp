using ArkCloud.Domain.Common;
using ArkCloud.Domain.Exceptions;
using ArkCloud.Domain.ValueObjects;

namespace ArkCloud.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; private set; }
    public string Sku { get; private set; }
    public Money UnitPrice { get; private set; }

    private Product()
    {
        Name = default!;
        Sku = default!;
        UnitPrice = default!;
    }

    private Product(string name, string sku, Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU is required.");

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        UnitPrice = unitPrice;
    }

    public static Product Create(string name, string sku, Money unitPrice)
        => new(name, sku, unitPrice);

    public void Update(string name, string sku, Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU is required.");

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        UnitPrice = unitPrice;
    }
}
