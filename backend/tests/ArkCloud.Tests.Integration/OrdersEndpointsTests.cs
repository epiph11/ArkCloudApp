using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Integration;

public class OrdersEndpointsTests : IClassFixture<ArkCloudApiFactory>, IAsyncLifetime
{
    private readonly ArkCloudApiFactory _factory;
    private HttpClient _client = null!;

    public OrdersEndpointsTests(ArkCloudApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => _client = await _factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CreateCustomerAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Order",
            LastName = "Buyer",
            Email = $"buyer.{Guid.NewGuid():N}@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });

        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        return body!.Id;
    }

    private async Task<Guid> CreateProductAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest
        {
            Name = "Widget",
            Sku = $"WID-{Guid.NewGuid():N}"[..12],
            UnitPrice = 25,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });

        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task Create_Should_Return_201_With_Computed_Total()
    {
        var customerId = await CreateCustomerAsync();
        var productId = await CreateProductAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest
        {
            CustomerId = customerId,
            Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 2 }]
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.TotalAmount.Should().Be(50);
        body.Status.Should().Be("Draft");
        body.CustomerName.Should().Be("Order Buyer");
        body.Items.Should().ContainSingle(i => i.ProductName == "Widget" && i.Quantity == 2);
    }

    [Fact]
    public async Task Create_Should_Return_404_When_Customer_Missing()
    {
        var productId = await CreateProductAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Should_Return_400_When_No_Items()
    {
        var customerId = await CreateCustomerAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest
        {
            CustomerId = customerId,
            Items = []
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAll_Should_Return_Paged_Result_Filtered_By_Status()
    {
        var customerId = await CreateCustomerAsync();
        var productId = await CreateProductAsync();

        var created = await _client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest
        {
            CustomerId = customerId,
            Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
        });
        var order = await created.Content.ReadFromJsonAsync<OrderResponse>();

        var response = await _client.GetAsync($"/api/v1/orders?search=Draft&page=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<OrderResponse>>();
        body!.Items.Should().Contain(o => o.Id == order!.Id);
    }

    [Fact]
    public async Task Submit_Then_Cancel_Should_Return_409()
    {
        var customerId = await CreateCustomerAsync();
        var productId = await CreateProductAsync();

        var created = await _client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest
        {
            CustomerId = customerId,
            Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
        });
        var order = await created.Content.ReadFromJsonAsync<OrderResponse>();

        var submitResponse = await _client.PostAsync($"/api/v1/orders/{order!.Id}/submit", content: null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondSubmit = await _client.PostAsync($"/api/v1/orders/{order.Id}/submit", content: null);
        secondSubmit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
