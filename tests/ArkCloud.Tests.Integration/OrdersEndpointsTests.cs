using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Integration;

public class OrdersEndpointsTests : IClassFixture<ArkCloudApiFactory>
{
    private readonly HttpClient _client;

    public OrdersEndpointsTests(ArkCloudApiFactory factory)
    {
        _client = factory.CreateClient();
    }

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
            Currency = "EUR"
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
