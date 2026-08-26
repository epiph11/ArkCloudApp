using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using ArkCloud.Domain.Common;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Integration;

public class ProductsEndpointsTests : IClassFixture<ArkCloudApiFactory>, IAsyncLifetime
{
    private readonly ArkCloudApiFactory _factory;
    private HttpClient _client = null!;

    public ProductsEndpointsTests(ArkCloudApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => _client = await _factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_Should_Return_201_With_Location_Header()
    {
        var request = new CreateProductRequest
        {
            Name = "Widget",
            Sku = $"WID-{Guid.NewGuid():N}"[..12],
            UnitPrice = 15,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAll_Should_Return_Paged_Result_Matching_Search()
    {
        var uniqueName = $"Zephyr{Guid.NewGuid():N}"[..20];

        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest
        {
            Name = uniqueName,
            Sku = $"SKU-{Guid.NewGuid():N}"[..12],
            UnitPrice = 10,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });

        var response = await _client.GetAsync($"/api/v1/products?search={uniqueName}&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ProductResponse>>();
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(p => p.Name == uniqueName);
    }

    [Fact]
    public async Task Update_Should_Return_403_For_Plain_User()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest
        {
            Name = "Original",
            Sku = $"SKU-{Guid.NewGuid():N}"[..12],
            UnitPrice = 10,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });
        var createdBody = await created.Content.ReadFromJsonAsync<ProductResponse>();

        var response = await _client.PutAsJsonAsync($"/api/v1/products/{createdBody!.Id}", new UpdateProductRequest
        {
            Name = "Renamed",
            Sku = createdBody.Sku,
            UnitPrice = 20,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_Should_Persist_Changes_For_Manager()
    {
        var managerClient = await _factory.CreateAuthenticatedClientAsync(RoleNames.Manager);

        var created = await managerClient.PostAsJsonAsync("/api/v1/products", new CreateProductRequest
        {
            Name = "Original",
            Sku = $"SKU-{Guid.NewGuid():N}"[..12],
            UnitPrice = 10,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });
        var createdBody = await created.Content.ReadFromJsonAsync<ProductResponse>();

        var response = await managerClient.PutAsJsonAsync($"/api/v1/products/{createdBody!.Id}", new UpdateProductRequest
        {
            Name = "Renamed",
            Sku = createdBody.Sku,
            UnitPrice = 20,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body!.Name.Should().Be("Renamed");
        body.UnitPrice.Should().Be(20);
    }

    [Fact]
    public async Task Delete_Should_Return_204_For_Admin_And_Then_404_On_GetById()
    {
        var adminClient = await _factory.CreateAuthenticatedClientAsync(RoleNames.Admin);

        var created = await adminClient.PostAsJsonAsync("/api/v1/products", new CreateProductRequest
        {
            Name = "Will be deleted",
            Sku = $"SKU-{Guid.NewGuid():N}"[..12],
            UnitPrice = 10,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });
        var createdBody = await created.Content.ReadFromJsonAsync<ProductResponse>();

        var deleteResponse = await adminClient.DeleteAsync($"/api/v1/products/{createdBody!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await adminClient.GetAsync($"/api/v1/products/{createdBody.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
