using ArkCloud.Domain.Exceptions;
using ArkCloud.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Unit.Domain;

public class ValueObjectsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    public void Email_Create_Should_Throw_For_Invalid_Values(string value)
    {
        var action = () => Email.Create(value);

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Email_Create_Should_Normalize_Value()
    {
        var email = Email.Create("  John.Doe@Example.com  ");

        email.Value.Should().Be("john.doe@example.com");
    }

    [Fact]
    public void Money_Create_Should_Throw_When_Amount_Is_Negative()
    {
        var action = () => Money.Create(-1, "EUR");

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Money_Create_Should_Normalize_Currency()
    {
        var money = Money.Create(10, "eur");

        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Address_Create_Should_Throw_When_Street_Is_Missing()
    {
        var action = () => Address.Create("", "Paris", "France");

        action.Should().Throw<DomainException>();
    }
}
