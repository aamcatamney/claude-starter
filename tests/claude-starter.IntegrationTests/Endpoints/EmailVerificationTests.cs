using System.Net;
using System.Net.Http.Json;
using claude_starter.IntegrationTests.Infrastructure;

namespace claude_starter.IntegrationTests.Endpoints;

[Collection(VerificationRequiredCollection.Name)]
public sealed class EmailVerificationTests : IAsyncLifetime
{
    private readonly VerificationRequiredFixture _fixture;
    private HttpClient _client = null!;

    public EmailVerificationTests(VerificationRequiredFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        _fixture.Factory.Emails.Clear();
        _client = _fixture.Factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private const string Password = "correct-horse-battery";

    private async Task<HttpResponseMessage> RegisterAsync(string email) =>
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = Password,
            displayName = (string?)null,
        });

    [Fact]
    public async Task Register_DoesNotSignInAndSendsVerification()
    {
        var response = await RegisterAsync("pending@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        // No session: login refuses unverified users, so registration must not
        // hand out what logging in would not.
        (await _client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var mail = _fixture.Factory.Emails.LastTo("pending@example.com");
        mail.Should().NotBeNull();
        mail!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_BeforeVerification_Returns403()
    {
        await RegisterAsync("unverified@example.com");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unverified@example.com",
            password = Password,
            rememberMe = false,
        });

        // 403, not 401: the credentials were right, the account is not ready.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("email-not-verified");
    }

    [Fact]
    public async Task Login_AfterVerification_Succeeds()
    {
        await RegisterAsync("verify-me@example.com");
        var token = _fixture.Factory.Emails.LastTo("verify-me@example.com")!.Token!;

        var verify = await _client.PostAsJsonAsync("/api/auth/verify-email", new { token });
        verify.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "verify-me@example.com",
            password = Password,
            rememberMe = false,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyEmail_TokenCannotBeUsedTwice()
    {
        await RegisterAsync("twice@example.com");
        var token = _fixture.Factory.Emails.LastTo("twice@example.com")!.Token!;

        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyEmail_UnknownToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", new { token = "not-a-real-token" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResendVerification_SendsAgainAndRetiresTheOldLink()
    {
        await RegisterAsync("resend@example.com");
        var first = _fixture.Factory.Emails.LastTo("resend@example.com")!.Token!;

        var resend = await _client.PostAsJsonAsync("/api/auth/resend-verification", new
        {
            email = "resend@example.com",
        });
        resend.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var second = _fixture.Factory.Emails.LastTo("resend@example.com")!.Token!;
        second.Should().NotBe(first);

        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token = first }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token = second }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResendVerification_UnknownAddress_IsIndistinguishable()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/resend-verification", new
        {
            email = "nobody@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _fixture.Factory.Emails.LastTo("nobody@example.com").Should().BeNull();
    }
}

/// <summary>
/// Verification is asked for, but SMTP is off. Honouring the request would mean
/// every account waiting forever on an email nothing can send.
/// </summary>
[Collection(VerificationWithoutSmtpCollection.Name)]
public sealed class VerificationWithoutSmtpTests : IAsyncLifetime
{
    private readonly VerificationWithoutSmtpFixture _fixture;
    private HttpClient _client = null!;

    public VerificationWithoutSmtpTests(VerificationWithoutSmtpFixture fixture)
    {
        _fixture = fixture;
    }

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

    [Fact]
    public async Task Register_SignsInDespiteVerificationBeingRequested()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "nosmtp@example.com",
            password = "correct-horse-battery",
            displayName = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_IsNotBlockedByVerification()
    {
        await _fixture.Seeder.CreateUserAsync("nosmtp-login@example.com", "correct-horse-battery");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nosmtp-login@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
