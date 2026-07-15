using ArkCloud.Blazor.Components.Layout;
using Bunit;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Component.Layout;

public class NavMenuTests : BunitContext
{
    [Fact]
    public void Dashboard_Link_Is_Always_Visible()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("Dashboard");
    }

    [Fact]
    public void Anonymous_User_Does_Not_See_The_Customers_Link()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Markup.Should().NotContain("Customers");
    }

    [Fact]
    public void Authenticated_User_Sees_The_Customers_Link()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("Customers");
    }

    [Fact]
    public void Anonymous_User_Does_Not_See_The_Products_Link()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Markup.Should().NotContain("Products");
    }

    [Fact]
    public void Authenticated_User_Sees_The_Products_Link()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("Products");
    }

    [Fact]
    public void Anonymous_User_Does_Not_See_The_Orders_Link()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Markup.Should().NotContain("Orders");
    }

    [Fact]
    public void Authenticated_User_Sees_The_Orders_Link()
    {
        AddAuthorization().SetAuthorized("user@example.com");

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("Orders");
    }
}
