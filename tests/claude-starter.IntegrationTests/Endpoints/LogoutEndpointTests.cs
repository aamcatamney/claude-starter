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

    // The SPA signs in and stays on the page: nothing calls /me in between, so
    // the only antiforgery token the client has is the one the sign-in response
    // issued. These two cover that path, which the /me-in-the-middle test above
    // does not.
    [Fact]
    public async Task Logout_WithXsrfFromRegisterResponse_Returns204()
    {
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "fresh-register@example.com",
            password = "correct-horse-battery",
            displayName = (string?)null,
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var xsrf = registerResponse.GetSetCookieValue(AuthEndpoints.XsrfCookieName);
        xsrf.Should().NotBeNullOrEmpty();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_WithXsrfFromLoginResponse_Returns204()
    {
        await Seeder.CreateUserAsync("fresh-login@example.com", "correct-horse-battery");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "fresh-login@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var xsrf = loginResponse.GetSetCookieValue(AuthEndpoints.XsrfCookieName);
        xsrf.Should().NotBeNullOrEmpty();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // Logout must not be able to fail silently. A rejected token used to leave
    // the auth cookie in place while the client cleared its own state, so the
    // user looked signed out while the session stayed live.
    [Fact]
    public async Task Logout_WithoutXsrf_SignsOutAnyway()
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
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await Client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithBogusXsrf_SignsOutAnyway()
    {
        await Seeder.CreateUserAsync("bogus@example.com", "correct-horse-battery");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "bogus@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", "not-a-real-token");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await Client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithValidXsrf_EndsTheSession()
    {
        await Seeder.CreateUserAsync("ends@example.com", "correct-horse-battery");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ends@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var xsrf = loginResponse.GetSetCookieValue(AuthEndpoints.XsrfCookieName);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        (await Client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await Client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
