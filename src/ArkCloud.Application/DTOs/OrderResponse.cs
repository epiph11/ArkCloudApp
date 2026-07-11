namespace ArkCloud.Application.DTOs;

public class OrderResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = default!;
    public decimal TotalAmount { get; set; }
}
