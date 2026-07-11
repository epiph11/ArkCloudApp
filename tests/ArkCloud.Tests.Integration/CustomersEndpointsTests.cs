using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Integration;

public class CustomersEndpointsTests : IClassFixture<ArkCloudApiFactory>
{
    private readonly HttpClient _client;

    public CustomersEndpointsTests(ArkCloudApiFactory factory)
    {
        _client = factory.CreateClient();
    }

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
}
