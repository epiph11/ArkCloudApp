using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Integration;

public class DashboardEndpointsTests : IClassFixture<ArkCloudApiFactory>, IAsyncLifetime
{
    private readonly ArkCloudApiFactory _factory;
    private HttpClient _client = null!;

    public DashboardEndpointsTests(ArkCloudApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => _client = await _factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_Should_Reflect_Newly_Created_Customer_In_Counts_And_Latest()
    {
        var uniqueLastName = $"Dashboard{Guid.NewGuid():N}"[..20];

        await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Newest",
            LastName = uniqueLastName,
            Email = $"newest.{Guid.NewGuid():N}@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });

        var response = await _client.GetAsync("/api/v1/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        body!.TotalCustomers.Should().BeGreaterThanOrEqualTo(1);
        body.LatestCustomers.Should().Contain(c => c.LastName == uniqueLastName);
    }

    [Fact]
    public async Task Get_Should_Return_401_When_Unauthenticated()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/v1/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
