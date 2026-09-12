using ArkCloud.Application.Interfaces;
using ArkCloud.Application.Services;
using ArkCloud.Domain.Entities;
using ArkCloud.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ArkCloud.Tests.Unit.Application;

/// <summary>
/// Unit-level coverage for CustomerRetentionPurgeService's own logic (cutoff computation,
/// anonymize-then-save, short-circuit on an empty result) with a mocked repository. The
/// eligibility query itself (correlated Max() subquery, real SQL translation) is covered
/// separately by CustomerRetentionPurgeIntegrationTests against a real Postgres — a mock can't
/// meaningfully exercise that part. See docs/rgpd-classification-donnees.md §2/§5, ADR-0012.
/// </summary>
public class CustomerRetentionPurgeServiceTests
{
    private readonly Mock<ICustomerRepository> _customerRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CustomerRetentionPurgeService _sut;

    public CustomerRetentionPurgeServiceTests()
    {
        _sut = new CustomerRetentionPurgeService(
            _customerRepository.Object, _unitOfWork.Object, NullLogger<CustomerRetentionPurgeService>.Instance)
        {
            RetentionYears = 3
        };
    }

    private static Customer NewCustomer() => Customer.Create(
        "John", "Doe", Email.Create($"john.{Guid.NewGuid():N}@example.com"),
        Address.Create("St", "Paris", "France"));

    [Fact]
    public async Task PurgeInactiveCustomersAsync_Should_Anonymize_Every_Eligible_Customer_And_Save_Once()
    {
        var customerA = NewCustomer();
        var customerB = NewCustomer();

        _customerRepository
            .Setup(x => x.GetEligibleForRetentionPurgeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([customerA, customerB]);

        var count = await _sut.PurgeInactiveCustomersAsync();

        count.Should().Be(2);
        customerA.FirstName.Should().Be("Anonymized");
        customerB.FirstName.Should().Be("Anonymized");
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeInactiveCustomersAsync_Should_Not_Call_SaveChanges_When_Nothing_Is_Eligible()
    {
        _customerRepository
            .Setup(x => x.GetEligibleForRetentionPurgeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var count = await _sut.PurgeInactiveCustomersAsync();

        count.Should().Be(0);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeInactiveCustomersAsync_Should_Pass_A_Cutoff_Consistent_With_RetentionYears()
    {
        DateTime? capturedCutoff = null;

        _customerRepository
            .Setup(x => x.GetEligibleForRetentionPurgeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((cutoff, _) => capturedCutoff = cutoff)
            .ReturnsAsync([]);

        await _sut.PurgeInactiveCustomersAsync();

        capturedCutoff.Should().NotBeNull();
        var expected = DateTime.UtcNow.AddYears(-3);
        capturedCutoff!.Value.Should().BeCloseTo(expected, TimeSpan.FromMinutes(1));
    }
}
