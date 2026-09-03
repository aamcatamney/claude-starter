using claude_starter.Repositories;
using claude_starter.Services.Auth;

namespace claude_starter.Endpoints.Auth;

public static class ResendVerificationEndpoint
{
    public sealed record ResendVerificationRequest(string Email);

    public static IEndpointRouteBuilder MapResendVerificationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/resend-verification", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        ResendVerificationRequest request,
        HttpContext http,
        IUserRepository users,
        EmailLinkService links,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.ResendVerification");
        var email = (request.Email ?? string.Empty).Trim();

        var user = string.IsNullOrEmpty(email) ? null : await users.GetByEmailAsync(email, ct);

        if (user is not null && user.IsActive && !user.EmailVerified)
        {
            await links.SendVerificationAsync(user, http.Request, ct);
            logger.LogInformation("Verification email resent. UserId={UserId}", user.Id);
        }

        // Same answer either way, for the same reason as forgot-password.
        return Results.Accepted();
    }
}
