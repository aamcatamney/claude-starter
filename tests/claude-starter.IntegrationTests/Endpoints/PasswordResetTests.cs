using System.Net;
using System.Net.Http.Json;
using claude_starter.IntegrationTests.Infrastructure;

namespace claude_starter.IntegrationTests.Endpoints;

[Collection(PasswordResetCollection.Name)]
public sealed class PasswordResetTests : IntegrationTestBase
{
    public PasswordResetTests(DatabaseFixture fixture) : base(fixture) { }

    private const string OldPassword = "correct-horse-battery";
    private const string NewPassword = "an-entirely-different-one";

    private async Task<string> RequestResetAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var mail = Fixture.Factory.Emails.LastTo(email);
        mail.Should().NotBeNull();
        mail!.Token.Should().NotBeNullOrEmpty();
        return mail.Token!;
    }

    [Fact]
    public async Task ForgotPassword_UnknownAddress_IsIndistinguishableFromKnown()
    {
        Fixture.Factory.Emails.Clear();

        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = "nobody@example.com",
        });

        // Same status as the success path — anything else tells a caller
        // whether an address has an account here.
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Fixture.Factory.Emails.LastTo("nobody@example.com").Should().BeNull();
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ReplacesThePassword()
    {
        await Seeder.CreateUserAsync("reset-me@example.com", OldPassword);
        var token = await RequestResetAsync("reset-me@example.com");

        var reset = await Client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token,
            password = NewPassword,
        });
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var withOld = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "reset-me@example.com",
            password = OldPassword,
            rememberMe = false,
        });
        withOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var withNew = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "reset-me@example.com",
            password = NewPassword,
            rememberMe = false,
        });
        withNew.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_TokenCannotBeUsedTwice()
    {
        await Seeder.CreateUserAsync("once-only@example.com", OldPassword);
        var token = await RequestResetAsync("once-only@example.com");

        var first = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = NewPassword });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "yet-another-password" });
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_RequestingAgain_RetiresTheEarlierLink()
    {
        await Seeder.CreateUserAsync("superseded@example.com", OldPassword);
        var first = await RequestResetAsync("superseded@example.com");
        var second = await RequestResetAsync("superseded@example.com");
        second.Should().NotBe(first);

        var withFirst = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token = first, password = NewPassword });
        withFirst.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var withSecond = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token = second, password = NewPassword });
        withSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // The reason to reset a password is often that somebody else knows it.
    // A session opened before the reset must not survive it.
    [Fact]
    public async Task ResetPassword_EndsSessionsOpenedBeforeIt()
    {
        await Seeder.CreateUserAsync("evict@example.com", OldPassword);

        var intruder = Fixture.Factory.CreateClient();
        var login = await intruder.PostAsJsonAsync("/api/auth/login", new
        {
            email = "evict@example.com",
            password = OldPassword,
            rememberMe = true,
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await intruder.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await RequestResetAsync("evict@example.com");
        var reset = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = NewPassword });
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await intruder.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_ShortPassword_Returns400()
    {
        await Seeder.CreateUserAsync("tooshort@example.com", OldPassword);
        var token = await RequestResetAsync("tooshort@example.com");

        var response = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "short" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
