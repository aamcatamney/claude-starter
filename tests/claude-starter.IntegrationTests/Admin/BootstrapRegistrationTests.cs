using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using claude_starter.IntegrationTests.Infrastructure;
using claude_starter.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace claude_starter.IntegrationTests.Admin;

[Collection(ClosedRegistrationCollection.Name)]
public sealed class BootstrapRegistrationTests : IAsyncLifetime
{
    private readonly ClosedRegistrationFixture _fixture;
    private HttpClient _client = null!;

    public BootstrapRegistrationTests(ClosedRegistrationFixture fixture) => _fixture = fixture;

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

    private string IssueInvite() =>
        _fixture.Factory.Services.GetRequiredService<BootstrapInviteService>().Issue();

    private Task<HttpResponseMessage> RegisterAsync(string email, string? inviteToken) =>
        _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = Password,
            displayName = (string?)null,
            inviteToken,
        });

    [Fact]
    public async Task Register_WithoutAnInvite_Returns403()
    {
        var response = await RegisterAsync("uninvited@example.com", inviteToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_WithAGarbageInvite_Returns403()
    {
        var response = await RegisterAsync("forged@example.com", inviteToken: "not-a-real-token");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_WithTheBootstrapInvite_CreatesAnAdministrator()
    {
        var response = await RegisterAsync("first@example.com", IssueInvite());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("isAdmin").GetBoolean().Should().BeTrue();

        // The claim travels with the session, not just the response body.
        var me = await _client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
    }

    // The invite is signed rather than stored, so the only thing that closes it
    // is an account existing. This is that guard.
    [Fact]
    public async Task Register_WithAValidInvite_IsRefusedOnceAnAccountExists()
    {
        var invite = IssueInvite();
        (await RegisterAsync("first-in@example.com", invite)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = _fixture.Factory.CreateClient();
        var response = await second.PostAsJsonAsync("/api/auth/register", new
        {
            email = "second-in@example.com",
            password = Password,
            displayName = (string?)null,
            inviteToken = invite,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Config_ReportsRegistrationClosed()
    {
        var config = await _client.GetFromJsonAsync<JsonElement>("/api/config");

        config.GetProperty("publicRegistrationEnabled").GetBoolean().Should().BeFalse();
    }
}

[Collection(AdminFlagCollection.Name)]
public sealed class AdminFlagTests : IntegrationTestBase
{
    public AdminFlagTests(DatabaseFixture fixture) : base(fixture) { }

    // Registration is open in this collection, so nobody registered through it
    // should come out privileged.
    [Fact]
    public async Task PublicRegistration_DoesNotCreateAdministrators()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "ordinary@example.com",
            password = "correct-horse-battery",
            displayName = (string?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await Client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("isAdmin").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Config_ReportsRegistrationOpen()
    {
        var config = await Client.GetFromJsonAsync<JsonElement>("/api/config");

        config.GetProperty("publicRegistrationEnabled").GetBoolean().Should().BeTrue();
    }
}
