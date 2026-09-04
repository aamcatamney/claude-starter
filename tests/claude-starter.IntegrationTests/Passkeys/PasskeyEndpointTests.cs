using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using claude_starter.Endpoints.Auth;
using claude_starter.IntegrationTests.Infrastructure;

namespace claude_starter.IntegrationTests.Passkeys;

[Collection(PasskeysEnabledCollection.Name)]
public sealed class PasskeyEndpointTests : IAsyncLifetime
{
    private readonly PasskeysEnabledFixture _fixture;
    private HttpClient _client = null!;

    public PasskeyEndpointTests(PasskeysEnabledFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        _client = _fixture.Factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private const string Password = "correct-horse-battery";

    private async Task SignInAsync(string email)
    {
        await _fixture.Seeder.CreateUserAsync(email, Password);
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = Password,
            rememberMe = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterOptions_RequiresASession()
    {
        var response = await _client.PostAsync("/api/auth/passkeys/register-options", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterOptions_ReturnsAChallengeAndStashesIt()
    {
        await SignInAsync("passkey-options@example.com");

        var response = await _client.PostAsync("/api/auth/passkeys/register-options", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("challenge").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("rp").GetProperty("id").GetString().Should().Be("localhost");
        // Discoverable: the whole point is signing in without typing an email.
        root.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString().Should().Be("required");

        // The challenge is remembered in an encrypted cookie, not a table.
        response.Headers.GetValues("Set-Cookie")
            .Should().Contain(c => c.StartsWith("passkey-registration="));
    }

    [Fact]
    public async Task SignInOptions_OffersNoCredentials()
    {
        var response = await _client.PostAsync("/api/auth/passkeys/sign-in-options", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // An empty list is what makes the authenticator offer its own accounts,
        // and is why this endpoint needs no email and leaks nothing.
        document.RootElement.GetProperty("allowCredentials").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Register_WithoutAChallenge_Returns400()
    {
        await SignInAsync("passkey-nochallenge@example.com");

        var response = await _client.PostAsJsonAsync("/api/auth/passkeys/register", new
        {
            response = new { id = "abc", rawId = "YWJj", type = "public-key", response = new { attestationObject = "", clientDataJSON = "" } },
            name = "Test key",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_StartsEmpty()
    {
        await SignInAsync("passkey-list@example.com");

        var response = await _client.GetAsync("/api/auth/passkeys");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaries = await response.Content.ReadFromJsonAsync<List<PasskeyListEndpoint.PasskeySummary>>();
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        await SignInAsync("passkey-delete@example.com");

        var meResponse = await _client.GetAsync("/api/auth/me");
        var xsrf = meResponse.GetSetCookieValue(AuthEndpoints.XsrfCookieName);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/auth/passkeys/{Guid.NewGuid()}");
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Config_ReportsPasskeysEnabled()
    {
        var response = await _client.GetFromJsonAsync<JsonElement>("/api/config");

        response.GetProperty("passkeysEnabled").GetBoolean().Should().BeTrue();
    }
}

/// <summary>The default posture: nothing mapped, nothing advertised.</summary>
[Collection(PasskeysDisabledCollection.Name)]
public sealed class PasskeysDisabledTests : IntegrationTestBase
{
    public PasskeysDisabledTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SignInOptions_IsNotRouted()
    {
        var response = await Client.PostAsync("/api/auth/passkeys/sign-in-options", content: null);

        // 405 rather than 404: the SPA fallback claims every unmatched path for
        // GET, so an unrouted POST is "method not allowed". Either way nothing
        // handles it, which is the claim being made.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Config_ReportsPasskeysDisabled()
    {
        var response = await Client.GetFromJsonAsync<JsonElement>("/api/config");

        response.GetProperty("passkeysEnabled").GetBoolean().Should().BeFalse();
    }
}
