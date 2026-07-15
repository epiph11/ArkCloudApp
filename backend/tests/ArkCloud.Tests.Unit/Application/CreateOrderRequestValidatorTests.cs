using ArkCloud.Application.DTOs;
using ArkCloud.Application.Validators;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Unit.Application;

public class CreateOrderRequestValidatorTests
{
    [Fact]
    public void Should_Fail_When_No_Items()
    {
        var validator = new CreateOrderRequestValidator();

        var result = validator.Validate(new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = []
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Item_Quantity_Is_Zero()
    {
        var validator = new CreateOrderRequestValidator();

        var result = validator.Validate(new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 0 }]
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_For_Valid_Request()
    {
        var validator = new CreateOrderRequestValidator();

        var result = validator.Validate(new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }]
        });

        result.IsValid.Should().BeTrue();
    }
}
