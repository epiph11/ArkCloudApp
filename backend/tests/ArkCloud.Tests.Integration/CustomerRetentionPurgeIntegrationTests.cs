using ArkCloud.Application.Interfaces;
using ArkCloud.Application.Services;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.ValueObjects;
using ArkCloud.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ArkCloud.Tests.Integration;

/// <summary>
/// Exercises CustomerRetentionPurgeService against a real Postgres (Testcontainers), not a
/// mocked repository — the eligibility query
/// (CustomerRepository.GetEligibleForRetentionPurgeAsync) relies on a correlated Max()
/// subquery whose EF Core -> SQL translation is exactly the kind of thing a mock can't catch if
/// it's wrong. See docs/rgpd-classification-donnees.md §2/§5, ADR-0012.
/// </summary>
public class CustomerRetentionPurgeIntegrationTests : IClassFixture<ArkCloudApiFactory>
{
    private readonly ArkCloudApiFactory _factory;

    public CustomerRetentionPurgeIntegrationTests(ArkCloudApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PurgeInactiveCustomersAsync_Should_Anonymize_Only_The_Customer_Inactive_Past_The_Threshold()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArkCloudDbContext>();
        var customerRepository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Three cases: an order older than the 3-year threshold (should be purged), an order
        // within the threshold (should be spared even though it's the same customer "shape" as
        // the first), and a customer with no order at all but created recently (should be
        // spared — no order does not mean "purge immediately", it falls back to Customer.CreatedAt).
        var inactiveCustomer = Customer.Create(
            "Old", "Customer", Email.Create($"old.{Guid.NewGuid():N}@example.com"),
            Address.Create("St", "Paris", "France"));
        var activeCustomer = Customer.Create(
            "Recent", "Customer", Email.Create($"recent.{Guid.NewGuid():N}@example.com"),
            Address.Create("St", "Paris", "France"));
        var neverOrderedNewCustomer = Customer.Create(
            "New", "Customer", Email.Create($"new.{Guid.NewGuid():N}@example.com"),
            Address.Create("St", "Paris", "France"));

        await customerRepository.AddAsync(inactiveCustomer);
        await customerRepository.AddAsync(activeCustomer);
        await customerRepository.AddAsync(neverOrderedNewCustomer);
        await context.SaveChangesAsync();

        var oldOrder = Order.Create(inactiveCustomer.Id);
        var recentOrder = Order.Create(activeCustomer.Id);
        context.Orders.AddRange(oldOrder, recentOrder);
        await context.SaveChangesAsync();

        // Order.CreatedAt is set once at construction (DateTime.UtcNow) with no public setter —
        // raw SQL is the only way to seed a historical date for a deterministic test.
        var fourYearsAgo = DateTime.UtcNow.AddYears(-4);
        var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE orders SET \"CreatedAt\" = {fourYearsAgo} WHERE \"Id\" = {oldOrder.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE orders SET \"CreatedAt\" = {oneMonthAgo} WHERE \"Id\" = {recentOrder.Id}");

        var purgeService = new CustomerRetentionPurgeService(
            customerRepository, unitOfWork, NullLogger<CustomerRetentionPurgeService>.Instance)
        {
            RetentionYears = 3
        };

        var purgedCount = await purgeService.PurgeInactiveCustomersAsync();

        purgedCount.Should().Be(1);

        var refreshedInactive = await customerRepository.GetByIdAsync(inactiveCustomer.Id);
        var refreshedActive = await customerRepository.GetByIdAsync(activeCustomer.Id);
        var refreshedNew = await customerRepository.GetByIdAsync(neverOrderedNewCustomer.Id);

        refreshedInactive!.FirstName.Should().Be("Anonymized");
        refreshedInactive.Email.Value.Should().EndWith("@arkcloud.invalid");
        refreshedActive!.FirstName.Should().Be("Recent");
        refreshedNew!.FirstName.Should().Be("New");

        // Idempotency: the customer just anonymized must not be picked up again on a re-run.
        var secondRunCount = await purgeService.PurgeInactiveCustomersAsync();
        secondRunCount.Should().Be(0);
    }
}
