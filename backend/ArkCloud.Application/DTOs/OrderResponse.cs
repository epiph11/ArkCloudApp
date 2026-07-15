namespace ArkCloud.Application.DTOs;

public class OrderResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Populated by the list/detail/create paths (which enrich via a batch customer lookup).
    /// Left blank by the bare Dashboard mapping, which only needs Status/TotalAmount.</summary>
    public string CustomerName { get; set; } = string.Empty;

    public string Status { get; set; } = default!;
    public decimal TotalAmount { get; set; }

    /// <summary>Populated the same way as CustomerName — empty for the Dashboard's bare mapping.</summary>
    public List<OrderItemResponse> Items { get; set; } = [];
}

public class OrderItemResponse
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
