using System.Net;
using System.Net.Http.Json;
using claude_starter.Endpoints.Auth;
using claude_starter.IntegrationTests.Infrastructure;

namespace claude_starter.IntegrationTests.Endpoints;

public sealed class LoginEndpointTests : IntegrationTestBase
{
    public LoginEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_AndIssuesCookies()
    {
        await Seeder.CreateUserAsync("alice@example.com", "correct-horse-battery", "Alice");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "alice@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginEndpoint.UserResponse>();
        body!.Email.Should().Be("alice@example.com");
        body.DisplayName.Should().Be("Alice");

        response.Headers.GetValues("Set-Cookie")
            .Should().Contain(c => c.StartsWith(".AspNetCore.Cookies"));
        response.GetSetCookieValue(AuthEndpoints.XsrfCookieName).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await Seeder.CreateUserAsync("alice@example.com", "correct-horse-battery");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "alice@example.com",
            password = "wrong-password",
            rememberMe = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InactiveUser_Returns401()
    {
        await Seeder.CreateUserAsync("inactive@example.com", "correct-horse-battery", isActive: false);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "inactive@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_MissingFields_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "",
            password = "",
            rememberMe = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
