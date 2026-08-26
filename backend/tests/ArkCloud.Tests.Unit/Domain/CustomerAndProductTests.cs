using ArkCloud.Domain.Entities;
using ArkCloud.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Unit.Domain;

public class CustomerAndProductTests
{
    [Fact]
    public void Should_Create_Customer_With_Valid_Data()
    {
        var customer = Customer.Create(
            "John",
            "Doe",
            Email.Create("john.doe@example.com"),
            Address.Create("Street 1", "Paris", "France"));

        customer.FirstName.Should().Be("John");
        customer.Email.Value.Should().Be("john.doe@example.com");
    }

    [Fact]
    public void Should_Update_Customer_Details()
    {
        var customer = Customer.Create(
            "John",
            "Doe",
            Email.Create("john.doe@example.com"),
            Address.Create("Street 1", "Paris", "France"));

        customer.Update(
            "Jane",
            "Doe",
            Email.Create("jane.doe@example.com"),
            Address.Create("Street 2", "Lyon", "France"));

        customer.FirstName.Should().Be("Jane");
        customer.Address.City.Should().Be("Lyon");
    }

    [Fact]
    public void Should_Create_Product_And_Uppercase_Sku()
    {
        var product = Product.Create("Widget", "wid-001", Money.Create(9.99m, "eur"), 10, "Gadgets");

        product.Sku.Should().Be("WID-001");
        product.UnitPrice.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Should_Update_Product_Details()
    {
        var product = Product.Create("Widget", "WID-001", Money.Create(9.99m, "EUR"), 10, "Gadgets");

        product.Update("Widget Pro", "WID-002", Money.Create(19.99m, "USD"), 5, "Premium Gadgets");

        product.Name.Should().Be("Widget Pro");
        product.Stock.Should().Be(5);
        product.Category.Should().Be("Premium Gadgets");
    }

    [Fact]
    public void Should_Reject_Negative_Stock()
    {
        var action = () => Product.Create("Widget", "WID-001", Money.Create(9.99m, "EUR"), -1, "Gadgets");

        action.Should().Throw<ArkCloud.Domain.Exceptions.DomainException>();
    }
}
