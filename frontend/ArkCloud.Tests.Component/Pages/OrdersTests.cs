using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using ArkCloud.Blazor.Components.Pages;
using ArkCloud.Tests.Component.TestSupport;
using Bunit;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Component.Pages;

public class OrdersTests : OrdersTestContext
{
    private static PagedResult<OrderResponse> OnePageOf(params OrderResponse[] items) => new()
    {
        Items = [.. items],
        TotalCount = items.Length,
        Page = 1,
        PageSize = 20
    };

    private static OrderResponse SampleOrder(Guid? id = null, string status = "Draft") => new()
    {
        Id = id ?? Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerName = "John Doe",
        Status = status,
        TotalAmount = 30,
        Items =
        [
            new OrderItemResponse { ProductId = Guid.NewGuid(), ProductName = "Widget", Sku = "WID-001", Quantity = 2, UnitPrice = 15, LineTotal = 30 }
        ]
    };

    [Fact]
    public void List_Renders_Orders_From_Paged_Response()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        FakeOrdersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf(SampleOrder()))
        };

        var cut = Render<Orders>();

        cut.Markup.Should().Contain("John Doe");
        cut.Markup.Should().Contain("Draft");
    }

    [Fact]
    public void Details_Renders_Items_And_Total()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var order = SampleOrder();
        FakeOrdersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(order)
        };

        var cut = Render<OrderDetails>(parameters => parameters.Add(p => p.Id, order.Id));

        cut.Markup.Should().Contain("Widget");
        cut.Markup.Should().Contain("30.00");
    }

    [Fact]
    public void Details_Shows_Submit_Button_For_Draft_Order()
    {
        AddAuthorization().SetAuthorized("user@example.com").SetRoles("User");

        var order = SampleOrder(status: "Draft");
        FakeOrdersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(order)
        };

        var cut = Render<OrderDetails>(parameters => parameters.Add(p => p.Id, order.Id));

        cut.Markup.Should().Contain("Submit order");
    }

    [Fact]
    public void Details_Hides_Cancel_Button_For_Plain_User_On_Submitted_Order()
    {
        AddAuthorization().SetAuthorized("user@example.com").SetRoles("User");

        var order = SampleOrder(status: "Submitted");
        FakeOrdersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(order)
        };

        var cut = Render<OrderDetails>(parameters => parameters.Add(p => p.Id, order.Id));

        cut.Markup.Should().NotContain("Cancel order");
    }

    [Fact]
    public void Details_Shows_Cancel_Button_For_Manager_On_Submitted_Order()
    {
        AddAuthorization().SetAuthorized("manager@example.com").SetRoles("Manager");

        var order = SampleOrder(status: "Submitted");
        FakeOrdersHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(order)
        };

        var cut = Render<OrderDetails>(parameters => parameters.Add(p => p.Id, order.Id));

        cut.Markup.Should().Contain("Cancel order");
    }

    [Fact]
    public void Create_Disables_Submit_Until_Customer_And_Product_Are_Chosen()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var cut = Render<OrderCreate>();

        var createButton = cut.FindAll("button").First(b => b.TextContent.Contains("Create order"));
        createButton.HasAttribute("disabled").Should().BeTrue("no customer or products have been selected yet");
    }
}
