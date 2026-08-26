using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs.Auth;
using ArkCloud.Blazor.Components.Pages;
using ArkCloud.Tests.Component.TestSupport;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArkCloud.Tests.Component.Pages;

public class LoginTests : AuthTestContext
{
    [Fact]
    public void Submitting_Empty_Form_Shows_Validation_Errors_And_Does_Not_Call_Api()
    {
        var apiWasCalled = false;
        FakeAuthHandler.Responder = _ =>
        {
            apiWasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        };

        var cut = Render<Login>();

        cut.Find("form").Submit();

        cut.FindAll(".validation-message").Should().NotBeEmpty("Email and Password are both [Required]");
        apiWasCalled.Should().BeFalse("client-side DataAnnotations validation must block the API call");
    }

    [Fact]
    public void Submitting_Valid_Credentials_Calls_Login_Endpoint_And_Navigates_Home()
    {
        HttpRequestMessage? capturedRequest = null;

        FakeAuthHandler.Responder = request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AuthResponse
                {
                    AccessToken = JwtTestTokens.CreateUnsigned("user@example.com"),
                    Expires = DateTime.UtcNow.AddHours(1),
                    RefreshToken = "fake-refresh-token",
                    RefreshTokenExpires = DateTime.UtcNow.AddDays(7),
                    Email = "user@example.com",
                    Roles = ["User"]
                })
            };
        };

        var cut = Render<Login>();

        var inputs = cut.FindAll("input");
        inputs[0].Change("user@example.com");            // Email (first InputText)
        cut.Find("input[type=password]").Change("Sup3rSecret!1");
        cut.Find("form").Submit();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsolutePath.Should().Be("/api/v1/auth/login");

        // Uri alone can't prove navigation happened, since a null ReturnUrl navigates back to
        // "/" which is also bUnit's default starting Uri — so assert on History instead.
        var navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.History.Should().ContainSingle("HandleLoginAsync should call NavigateTo exactly once on success");
    }

    [Fact]
    public void Failed_Login_Shows_The_Api_Error_Message_Instead_Of_Navigating()
    {
        FakeAuthHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent.Create(new { title = "Unauthorized", detail = "Invalid email or password." })
        };

        var cut = Render<Login>();

        var inputs = cut.FindAll("input");
        inputs[0].Change("user@example.com");
        cut.Find("input[type=password]").Change("wrong-password");
        cut.Find("form").Submit();

        cut.Find(".alert-danger").TextContent.Should().Contain("Invalid email or password.");

        var navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.History.Should().BeEmpty("the caught exception must prevent NavigateTo from ever being called");
    }
}
