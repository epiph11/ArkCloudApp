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
        var product = Product.Create("Widget", "wid-001", Money.Create(9.99m, "eur"));

        product.Sku.Should().Be("WID-001");
        product.UnitPrice.Currency.Should().Be("EUR");
    }
}
