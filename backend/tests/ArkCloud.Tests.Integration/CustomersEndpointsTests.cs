using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using ArkCloud.Domain.Common;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Integration;

public class CustomersEndpointsTests : IClassFixture<ArkCloudApiFactory>, IAsyncLifetime
{
    private readonly ArkCloudApiFactory _factory;
    private HttpClient _client = null!;

    public CustomersEndpointsTests(ArkCloudApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => _client = await _factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_Should_Return_201_With_Location_Header()
    {
        var request = new CreateCustomerRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = $"john.{Guid.NewGuid():N}@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        body!.Email.Should().Be(request.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task Create_Should_Return_400_When_Email_Invalid()
    {
        var request = new CreateCustomerRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "not-an-email",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_Should_Return_201_Created_Customer()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = $"jane.{Guid.NewGuid():N}@example.com",
            Street = "Street 2",
            City = "Lyon",
            Country = "France"
        });

        var createdBody = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var response = await _client.GetAsync($"/api/v1/customers/{createdBody!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        body!.Id.Should().Be(createdBody.Id);
    }

    [Fact]
    public async Task GetById_Should_Return_404_When_Customer_Missing()
    {
        var response = await _client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_Return_Paged_Result_Matching_Search()
    {
        var uniqueLastName = $"Zephyr{Guid.NewGuid():N}"[..20];

        await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Searchable",
            LastName = uniqueLastName,
            Email = $"searchable.{Guid.NewGuid():N}@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });

        var response = await _client.GetAsync($"/api/v1/customers?search={uniqueLastName}&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<CustomerResponse>>();
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(c => c.LastName == uniqueLastName);
    }

    [Fact]
    public async Task Update_Should_Persist_Changes()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Before",
            LastName = "Update",
            Email = $"before.{Guid.NewGuid():N}@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });
        var createdBody = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var response = await _client.PutAsJsonAsync($"/api/v1/customers/{createdBody!.Id}", new UpdateCustomerRequest
        {
            FirstName = "After",
            LastName = "Update",
            Email = createdBody.Email,
            Street = "Street 2",
            City = "Lyon",
            Country = "France"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        body!.FirstName.Should().Be("After");
        body.City.Should().Be("Lyon");
    }

    [Fact]
    public async Task Delete_Should_Return_403_For_NonAdmin_User()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Cannot",
            LastName = "Delete",
            Email = $"cannotdelete.{Guid.NewGuid():N}@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });
        var createdBody = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var response = await _client.DeleteAsync($"/api/v1/customers/{createdBody!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Should_Return_204_For_Admin_And_Then_404_On_GetById()
    {
        var adminClient = await _factory.CreateAuthenticatedClientAsync(RoleNames.Admin);

        var created = await adminClient.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Will",
            LastName = "BeDeleted",
            Email = $"willbedeleted.{Guid.NewGuid():N}@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });
        var createdBody = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var deleteResponse = await adminClient.DeleteAsync($"/api/v1/customers/{createdBody!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await adminClient.GetAsync($"/api/v1/customers/{createdBody.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
