using claude_starter.Models;
using claude_starter.Repositories;
using claude_starter.Services.Auth;

namespace claude_starter.Endpoints.Auth;

public static class VerifyEmailEndpoint
{
    public sealed record VerifyEmailRequest(string Token);

    public static IEndpointRouteBuilder MapVerifyEmailEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/verify-email", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        VerifyEmailRequest request,
        IUserRepository users,
        EmailLinkService links,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.VerifyEmail");

        var userId = await links.RedeemAsync(TokenPurpose.EmailVerification, request.Token ?? string.Empty, ct);
        if (userId is null)
        {
            logger.LogWarning("Email verification attempted with an unusable token.");
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid or expired link",
                detail: "Request a new verification link.");
        }

        await users.SetEmailVerifiedAsync(userId.Value, ct);

        logger.LogInformation("Email verified. UserId={UserId}", userId);
        return Results.NoContent();
    }
}
