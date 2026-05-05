using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using claude_starter.Endpoints.Auth;
using claude_starter.IntegrationTests.Infrastructure;

namespace claude_starter.IntegrationTests.Endpoints;

public sealed class LogoutEndpointTests : IntegrationTestBase
{
    public LogoutEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Logout_Unauthenticated_Returns401()
    {
        var response = await Client.PostAsync("/api/auth/logout", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_AuthenticatedWithXsrf_Returns204()
    {
        await Seeder.CreateUserAsync("bye@example.com", "correct-horse-battery");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "bye@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResponse = await Client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var xsrf = meResponse.GetSetCookieValue(AuthEndpoints.XsrfCookieName);
        xsrf.Should().NotBeNullOrEmpty();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_AuthenticatedWithoutXsrf_Returns400()
    {
        await Seeder.CreateUserAsync("noxsrf@example.com", "correct-horse-battery");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "noxsrf@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
