using ArkCloud.Application.DTOs;
using ArkCloud.Application.Validators;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Unit.Application;

public class CreateProductRequestValidatorTests
{
    [Fact]
    public void Should_Fail_When_Price_Is_Not_Positive()
    {
        var validator = new CreateProductRequestValidator();

        var result = validator.Validate(new CreateProductRequest
        {
            Name = "Widget",
            Sku = "WID-001",
            UnitPrice = 0,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_For_Valid_Request()
    {
        var validator = new CreateProductRequestValidator();

        var result = validator.Validate(new CreateProductRequest
        {
            Name = "Widget",
            Sku = "WID-001",
            UnitPrice = 9.99m,
            Currency = "EUR",
            Stock = 10,
            Category = "Gadgets"
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Fail_When_Stock_Is_Negative()
    {
        var validator = new CreateProductRequestValidator();

        var result = validator.Validate(new CreateProductRequest
        {
            Name = "Widget",
            Sku = "WID-001",
            UnitPrice = 9.99m,
            Currency = "EUR",
            Stock = -1,
            Category = "Gadgets"
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Category_Is_Empty()
    {
        var validator = new CreateProductRequestValidator();

        var result = validator.Validate(new CreateProductRequest
        {
            Name = "Widget",
            Sku = "WID-001",
            UnitPrice = 9.99m,
            Currency = "EUR",
            Stock = 10,
            Category = ""
        });

        result.IsValid.Should().BeFalse();
    }
}
