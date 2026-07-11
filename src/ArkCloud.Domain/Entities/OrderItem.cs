using ArkCloud.Domain.Common;
using ArkCloud.Domain.Exceptions;

namespace ArkCloud.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;

    private OrderItem() { }

    private OrderItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
            throw new DomainException("ProductId is required.");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");
        if (unitPrice <= 0)
            throw new DomainException("Unit price must be greater than zero.");

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public static OrderItem Create(Guid productId, int quantity, decimal unitPrice)
        => new(productId, quantity, unitPrice);
}
