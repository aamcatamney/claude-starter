using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using claude_starter.Repositories;
using claude_starter.Services.Auth;
using claude_starter.Services.Email;

namespace claude_starter.Endpoints.Auth;

public static class LoginEndpoint
{
    /// <summary>
    /// Problem type that marks the one failure a client should handle specially,
    /// rather than parsing the human-readable title.
    /// </summary>
    public const string EmailNotVerifiedType = "https://claude-starter/problems/email-not-verified";

    public sealed record LoginRequest(string Email, string Password, bool RememberMe);
    public sealed record UserResponse(Guid Id, string Email, string? DisplayName);

    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/login", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        LoginRequest request,
        HttpContext http,
        IUserRepository users,
        IPasswordHasher hasher,
        IAntiforgery antiforgery,
        IOptions<AuthOptions> authOptions,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.Login");
        var email = (request.Email ?? string.Empty).Trim();
        var ip = http.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password))
        {
            logger.LogWarning("Login failed (missing fields). Email={Email} IP={Ip}", email, ip);
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var user = await users.GetByEmailAsync(email, ct);
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed. Email={Email} IP={Ip}", email, ip);
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        // Right credentials, not allowed yet: 403 rather than 401, and a code
        // the client can act on by offering to resend the verification email.
        if (authOptions.Value.RequireEmailVerification && !user.EmailVerified)
        {
            logger.LogInformation("Login blocked, email not verified. UserId={UserId}", user.Id);
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Email not verified",
                detail: "Confirm your email address before signing in.",
                type: EmailNotVerifiedType);
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(AuthEndpoints.SecurityStampClaim, user.SecurityStamp.ToString()),
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        var props = new AuthenticationProperties
        {
            IsPersistent = request.RememberMe,
            ExpiresUtc = request.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null,
        };

        var principal = new ClaimsPrincipal(identity);

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            props);

        // SignInAsync only writes the response cookie; it leaves HttpContext.User
        // anonymous for the rest of this request. Antiforgery tokens are bound to
        // the current user, so minting one now would bind it to nobody and every
        // later authenticated request would reject it.
        http.User = principal;

        AuthEndpoints.IssueXsrfCookie(http, antiforgery);

        logger.LogInformation("Login success. UserId={UserId}", user.Id);

        return Results.Ok(new UserResponse(user.Id, user.Email, user.DisplayName));
    }
}
