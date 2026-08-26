using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs;
using ArkCloud.Blazor.Components.Pages;
using ArkCloud.Tests.Component.TestSupport;
using Bunit;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Component.Pages;

public class ProductsTests : ProductsTestContext
{
    private static PagedResult<ProductResponse> OnePageOf(params ProductResponse[] items) => new()
    {
        Items = [.. items],
        TotalCount = items.Length,
        Page = 1,
        PageSize = 20
    };

    private static ProductResponse SampleProduct(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Widget",
        Sku = "WID-001",
        UnitPrice = 15,
        Currency = "EUR",
        Stock = 10,
        Category = "Gadgets"
    };

    [Fact]
    public void List_Renders_Products_From_Paged_Response()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf(SampleProduct()))
        };

        var cut = Render<Products>();

        cut.Markup.Should().Contain("Widget");
        cut.Markup.Should().Contain("Gadgets");
    }

    [Fact]
    public void List_Hides_Delete_Button_For_Plain_User()
    {
        AddAuthorization().SetAuthorized("user@example.com").SetRoles("User");

        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf(SampleProduct()))
        };

        var cut = Render<Products>();

        cut.Markup.Should().NotContain("Delete");
    }

    [Fact]
    public void List_Shows_Delete_Button_For_Manager()
    {
        AddAuthorization().SetAuthorized("manager@example.com").SetRoles("Manager");

        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf(SampleProduct()))
        };

        var cut = Render<Products>();

        cut.Markup.Should().Contain("Delete");
    }

    [Fact]
    public void List_Always_Shows_New_Product_Button_For_Any_Authenticated_User()
    {
        AddAuthorization().SetAuthorized("user@example.com").SetRoles("User");

        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(OnePageOf())
        };

        var cut = Render<Products>();

        cut.Markup.Should().Contain("New product");
    }

    [Fact]
    public void Details_Renders_Product_Fields()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var product = SampleProduct();
        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(product)
        };

        var cut = Render<ProductDetails>(parameters => parameters.Add(p => p.Id, product.Id));

        cut.Markup.Should().Contain("Widget");
        cut.Markup.Should().Contain("WID-001");
        cut.Markup.Should().Contain("Gadgets");
    }

    [Fact]
    public void Details_Hides_Edit_Link_For_Plain_User()
    {
        AddAuthorization().SetAuthorized("user@example.com").SetRoles("User");

        var product = SampleProduct();
        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(product)
        };

        var cut = Render<ProductDetails>(parameters => parameters.Add(p => p.Id, product.Id));

        cut.Markup.Should().NotContain("Edit");
    }

    [Fact]
    public void Edit_Shows_Permission_Message_For_Plain_User()
    {
        AddAuthorization().SetAuthorized("user@example.com").SetRoles("User");

        var product = SampleProduct();
        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(product)
        };

        var cut = Render<ProductEdit>(parameters => parameters.Add(p => p.Id, product.Id));

        cut.Markup.Should().Contain("You don't have permission to edit products.");
    }

    [Fact]
    public void Edit_Prepopulates_Form_For_Manager()
    {
        AddAuthorization().SetAuthorized("manager@example.com").SetRoles("Manager");

        var product = SampleProduct();
        FakeProductsHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(product)
        };

        var cut = Render<ProductEdit>(parameters => parameters.Add(p => p.Id, product.Id));

        var inputs = cut.FindAll("input");
        inputs[0].GetAttribute("value").Should().Be("Widget");
        inputs[1].GetAttribute("value").Should().Be("WID-001");
    }

    [Fact]
    public void Create_Submits_New_Product()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var createCalled = false;
        FakeProductsHandler.Responder = request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                createCalled = true;
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(SampleProduct())
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var cut = Render<ProductCreate>();

        // Each Change() triggers a re-render, which reassigns Blazor's internal event handler
        // IDs — reusing a single FindAll() snapshot across multiple Change() calls throws
        // Bunit.Rendering.UnknownEventHandlerIdException on the second+ call. Re-querying the
        // DOM fresh before each interaction avoids stale element references.
        cut.FindAll("input")[0].Change("New Widget");   // Name
        cut.FindAll("input")[1].Change("NW-001");       // Sku
        cut.FindAll("input")[2].Change("Gadgets");      // Category
        cut.FindAll("input")[3].Change("9.99");         // UnitPrice
        cut.FindAll("input")[4].Change("EUR");          // Currency
        cut.FindAll("input")[5].Change("5");            // Stock

        cut.Find("form").Submit();

        createCalled.Should().BeTrue();
    }
}
