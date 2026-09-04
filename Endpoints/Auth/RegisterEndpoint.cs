using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using claude_starter.Repositories;
using claude_starter.Services.Auth;
using claude_starter.Services.Diagnostics;
using claude_starter.Services.Email;

namespace claude_starter.Endpoints.Auth;

public static class RegisterEndpoint
{
    private const int MinPasswordLength = 12;
    private const int MaxEmailLength = 254;

    public sealed record RegisterRequest(string Email, string Password, string? DisplayName, string? InviteToken);

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
        BootstrapInviteService invites,
        IOptions<AuthOptions> authOptions,
        AppMetrics metrics,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.Register");
        var email = (request.Email ?? string.Empty).Trim();

        // Three ways in, in order of precedence: registration is open to
        // everyone; or this is a genuine bootstrap invite and no account exists
        // yet, which makes the caller the first administrator; or the door is
        // shut.
        var isBootstrap = false;

        if (!authOptions.Value.AllowPublicRegistration)
        {
            var inviteAccepted = invites.IsValid(request.InviteToken) && !await users.AnyAsync(ct);

            if (!inviteAccepted)
            {
                logger.LogWarning("Registration refused: closed to the public and no usable invite.");
                metrics.Registration("refused");
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Registration is closed",
                    detail: "Ask an administrator for an account.");
            }

            isBootstrap = true;
        }

        if (email.Length == 0 || email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out _))
        {
            metrics.Registration("invalid");
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid email");
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
        {
            metrics.Registration("invalid");
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: $"Password must be at least {MinPasswordLength} characters.");
        }

        var existing = await users.GetByEmailAsync(email, ct);
        if (existing is not null)
        {
            metrics.Registration("duplicate");
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered");
        }

        var hash = hasher.Hash(request.Password);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName!.Trim();

        // The account created from a bootstrap invite is the administrator —
        // there is nobody else to grant it.
        var id = await users.CreateAsync(email, hash, displayName, isBootstrap, ct);
        var created = await users.GetByIdAsync(id, ct);

        if (authOptions.Value.RequireEmailVerification)
        {
            if (created is not null)
            {
                await links.SendVerificationAsync(created, http.Request, ct);
            }

            logger.LogInformation(
                "Register pending verification. UserId={UserId} Admin={Admin}", id, isBootstrap);
            metrics.Registration("pending-verification");

            // No session: login refuses unverified users, so handing one out
            // here would let registration do what logging in cannot.
            return Results.Accepted(value: new PendingVerificationResponse(email.ToLowerInvariant()));
        }

        if (created is not null)
        {
            await AuthEndpoints.SignInAsync(http, created, persistent: true, antiforgery);
        }

        logger.LogInformation("Register success. UserId={UserId} Admin={Admin}", id, isBootstrap);
        metrics.Registration("created");

        return Results.Ok(new LoginEndpoint.UserResponse(id, email.ToLowerInvariant(), displayName, isBootstrap));
    }
}
