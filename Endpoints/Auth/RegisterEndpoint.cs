using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using claude_starter.Repositories;
using claude_starter.Services.Auth;
using claude_starter.Services.Email;

namespace claude_starter.Endpoints.Auth;

public static class RegisterEndpoint
{
    private const int MinPasswordLength = 12;
    private const int MaxEmailLength = 254;

    public sealed record RegisterRequest(string Email, string Password, string? DisplayName);

    /// <summary>Returned instead of a session when verification is required.</summary>
    public sealed record PendingVerificationResponse(string Email)
    {
        public bool VerificationRequired => true;
    }

    public static IEndpointRouteBuilder MapRegisterEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/register", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        RegisterRequest request,
        HttpContext http,
        IUserRepository users,
        IPasswordHasher hasher,
        IAntiforgery antiforgery,
        EmailLinkService links,
        IOptions<AuthOptions> authOptions,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.Register");
        var email = (request.Email ?? string.Empty).Trim();

        if (email.Length == 0 || email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out _))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid email");
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: $"Password must be at least {MinPasswordLength} characters.");
        }

        var existing = await users.GetByEmailAsync(email, ct);
        if (existing is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered");
        }

        var hash = hasher.Hash(request.Password);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName!.Trim();

        var id = await users.CreateAsync(email, hash, displayName, ct);
        var created = await users.GetByIdAsync(id, ct);

        if (authOptions.Value.RequireEmailVerification)
        {
            if (created is not null)
            {
                await links.SendVerificationAsync(created, http.Request, ct);
            }

            logger.LogInformation("Register pending verification. UserId={UserId}", id);

            // No session: login refuses unverified users, so handing one out
            // here would let registration do what logging in cannot.
            return Results.Accepted(value: new PendingVerificationResponse(email.ToLowerInvariant()));
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email.ToLowerInvariant()),
            new Claim(AuthEndpoints.SecurityStampClaim, (created?.SecurityStamp ?? Guid.Empty).ToString()),
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });

        // SignInAsync only writes the response cookie; it leaves HttpContext.User
        // anonymous for the rest of this request. Antiforgery tokens are bound to
        // the current user, so minting one now would bind it to nobody and every
        // later authenticated request would reject it.
        http.User = principal;

        AuthEndpoints.IssueXsrfCookie(http, antiforgery);

        logger.LogInformation("Register success. UserId={UserId}", id);

        return Results.Ok(new LoginEndpoint.UserResponse(id, email.ToLowerInvariant(), displayName));
    }
}
