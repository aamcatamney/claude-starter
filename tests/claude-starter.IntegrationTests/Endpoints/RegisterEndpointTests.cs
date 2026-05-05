using System.Net;
using System.Net.Http.Json;
using claude_starter.Endpoints.Auth;
using claude_starter.IntegrationTests.Infrastructure;

namespace claude_starter.IntegrationTests.Endpoints;

public sealed class RegisterEndpointTests : IntegrationTestBase
{
    public RegisterEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Register_ValidRequest_Returns200_AndIssuesCookies()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "new@example.com",
            password = "correct-horse-battery",
            displayName = "New User",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginEndpoint.UserResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("new@example.com");
        body.DisplayName.Should().Be("New User");

        response.Headers.GetValues("Set-Cookie")
            .Should().Contain(c => c.StartsWith(".AspNetCore.Cookies"));
        response.GetSetCookieValue(AuthEndpoints.XsrfCookieName).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await Seeder.CreateUserAsync("dup@example.com", "correct-horse-battery");

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "dup@example.com",
            password = "another-strong-password",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "short@example.com",
            password = "short",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "correct-horse-battery",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
