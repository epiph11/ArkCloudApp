using ArkCloud.Application.DTOs;
using ArkCloud.Application.Validators;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Unit.Application;

public class CreateCustomerRequestValidatorTests
{
    [Fact]
    public void Should_Fail_When_Email_Is_Invalid()
    {
        var validator = new CreateCustomerRequestValidator();

        var result = validator.Validate(new CreateCustomerRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "bad-email",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_For_Valid_Request()
    {
        var validator = new CreateCustomerRequestValidator();

        var result = validator.Validate(new CreateCustomerRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Street = "Street 1",
            City = "Paris",
            Country = "France"
        });

        result.IsValid.Should().BeTrue();
    }
}
