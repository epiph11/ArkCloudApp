using ArkCloud.Domain.Common;
using ArkCloud.Domain.Enums;
using ArkCloud.Domain.Exceptions;

namespace ArkCloud.Domain.Entities;

public class Order : BaseEntity
{
    private readonly List<OrderItem> _items = [];

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public decimal TotalAmount => _items.Sum(x => x.LineTotal);

    private Order() { }

    private Order(Guid customerId)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("CustomerId is required.");

        CustomerId = customerId;
        Status = OrderStatus.Draft;
    }

    public static Order Create(Guid customerId) => new(customerId);

    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOrderStateException("Items can only be added to a draft order.");

        var item = OrderItem.Create(productId, quantity, unitPrice);
        _items.Add(item);
    }

    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOrderStateException("Only a draft order can be submitted.");

        if (_items.Count == 0)
            throw new DomainException("An order must contain at least one item.");

        Status = OrderStatus.Submitted;
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.Submitted)
            throw new InvalidOrderStateException("Only a submitted order can be marked as paid.");

        Status = OrderStatus.Paid;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Paid)
            throw new InvalidOrderStateException("A paid order cannot be cancelled.");

        if (Status == OrderStatus.Cancelled)
            throw new InvalidOrderStateException("Order is already cancelled.");

        Status = OrderStatus.Cancelled;
    }
}
