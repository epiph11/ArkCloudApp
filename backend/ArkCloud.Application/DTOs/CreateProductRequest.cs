namespace ArkCloud.Application.DTOs;

public class CreateProductRequest
{
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = default!;
    public int Stock { get; set; }
    public string Category { get; set; } = default!;
}
