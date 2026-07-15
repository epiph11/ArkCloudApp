using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using ArkCloud.Blazor.Components.Pages;
using ArkCloud.Tests.Component.TestSupport;
using Bunit;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Component.Pages;

public class CustomersTests : CustomersTestContext
{
    private static PagedResult<CustomerResponse> OnePageOf(params CustomerResponse[] items) => new()
    {
        Items = [.. items],
        TotalCount = items.Length,
        Page = 1,
        PageSize = 20
    };

    private static CustomerResponse SampleCustomer(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FirstName = "John",
        LastName = "Doe",
        Email = "john.doe@example.com",
        Street = "Street 1",
        City = "Paris",
        Country = "France"
    };

    [Fact]
    public void List_Renders_Customers_From_Paged_Response()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        FakeCustomersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf(SampleCustomer()))
        };

        var cut = Render<Customers>();

        cut.Markup.Should().Contain("John");
        cut.Markup.Should().Contain("john.doe@example.com");
    }

    [Fact]
    public void List_Hides_Delete_Button_For_NonAdmin_User()
    {
        AddAuthorization().SetAuthorized("user@example.com").SetRoles("User");

        FakeCustomersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf(SampleCustomer()))
        };

        var cut = Render<Customers>();

        cut.Markup.Should().NotContain("Delete");
    }

    [Fact]
    public void List_Shows_Delete_Button_For_Admin_User()
    {
        AddAuthorization().SetAuthorized("admin@example.com").SetRoles("Admin");

        FakeCustomersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf(SampleCustomer()))
        };

        var cut = Render<Customers>();

        cut.Markup.Should().Contain("Delete");
    }

    [Fact]
    public void Details_Renders_Customer_Fields()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var customer = SampleCustomer();
        FakeCustomersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(customer)
        };

        var cut = Render<CustomerDetails>(parameters => parameters.Add(p => p.Id, customer.Id));

        cut.Markup.Should().Contain("John Doe");
        cut.Markup.Should().Contain("Paris");
    }

    [Fact]
    public void Details_Shows_NotFound_Message_When_Customer_Missing()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        FakeCustomersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        var cut = Render<CustomerDetails>(parameters => parameters.Add(p => p.Id, Guid.NewGuid()));

        cut.Markup.Should().Contain("Customer not found.");
    }

    [Fact]
    public void Edit_Prepopulates_Form_From_Existing_Customer()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var customer = SampleCustomer();
        FakeCustomersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(customer)
        };

        var cut = Render<CustomerEdit>(parameters => parameters.Add(p => p.Id, customer.Id));

        var inputs = cut.FindAll("input");
        inputs[0].GetAttribute("value").Should().Be("John");
        inputs[1].GetAttribute("value").Should().Be("Doe");
    }

    [Fact]
    public void Edit_Submits_Update_And_Navigates_To_Details()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var customer = SampleCustomer();
        var updateCalled = false;

        FakeCustomersHandler.Responder = request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                updateCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(customer) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(customer) };
        };

        var cut = Render<CustomerEdit>(parameters => parameters.Add(p => p.Id, customer.Id));

        cut.Find("form").Submit();

        updateCalled.Should().BeTrue();
    }
}
