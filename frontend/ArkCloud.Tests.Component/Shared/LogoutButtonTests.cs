using System.Net;
using System.Net.Http.Json;
using ArkCloud.Application.DTOs.Auth;
using ArkCloud.Blazor.Auth;
using ArkCloud.Blazor.Components.Shared;
using ArkCloud.Blazor.Services;
using ArkCloud.Tests.Component.TestSupport;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArkCloud.Tests.Component.Shared;

public class LogoutButtonTests : AuthTestContext
{
    [Fact]
    public async Task Click_Revokes_The_Refresh_Token_Clears_Storage_And_Navigates_To_Login()
    {
        // Arrange: log in first, exactly like Login.razor would, so there's something to log out of.
        FakeAuthHandler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
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

        var authProvider = Services.GetRequiredService<JwtAuthenticationStateProvider>();
        await authProvider.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "Sup3rSecret!1" });

        HttpRequestMessage? logoutRequest = null;
        FakeAuthHandler.Responder = request =>
        {
            logoutRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        };

        var cut = Render<LogoutButton>();

        // Act
        cut.Find("button").Click();

        // Assert
        logoutRequest.Should().NotBeNull("clicking Logout must call POST /api/v1/auth/logout to revoke the refresh token");
        logoutRequest!.RequestUri!.AbsolutePath.Should().Be("/api/v1/auth/logout");

        var tokenStorage = Services.GetRequiredService<ITokenStorageService>();
        (await tokenStorage.GetAccessTokenAsync()).Should().BeNull("ClearAsync must run even if the server call fails/succeeds");

        var navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.History.Should().ContainSingle("LogoutAsync must navigate back to the login page exactly once");
    }
}
