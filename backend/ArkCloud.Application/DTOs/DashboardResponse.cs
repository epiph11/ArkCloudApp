namespace ArkCloud.Application.DTOs;

public class DashboardResponse
{
    public int TotalCustomers { get; set; }
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public OrderStatusCounts OrdersByStatus { get; set; } = new();

    public List<CustomerResponse> LatestCustomers { get; set; } = [];
    public List<ProductResponse> LatestProducts { get; set; } = [];
    public List<OrderResponse> LatestOrders { get; set; } = [];
}

public class OrderStatusCounts
{
    public int Draft { get; set; }
    public int Submitted { get; set; }
    public int Paid { get; set; }
    public int Cancelled { get; set; }
}
