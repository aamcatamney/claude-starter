using claude_starter.Models;
using claude_starter.Repositories;
using claude_starter.Services.Auth;

namespace claude_starter.Endpoints.Auth;

public static class ResetPasswordEndpoint
{
    private const int MinPasswordLength = 12;

    public sealed record ResetPasswordRequest(string Token, string Password);

    public static IEndpointRouteBuilder MapResetPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/reset-password", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        ResetPasswordRequest request,
        IUserRepository users,
        IPasswordHasher hasher,
        EmailLinkService links,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.ResetPassword");

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: $"Password must be at least {MinPasswordLength} characters.");
        }

        var userId = await links.RedeemAsync(TokenPurpose.PasswordReset, request.Token ?? string.Empty, ct);
        if (userId is null)
        {
            logger.LogWarning("Password reset attempted with an unusable token.");
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid or expired link",
                detail: "Request a new password reset link.");
        }

        // Rotates the security stamp, so any session opened before this stops
        // working, and marks the address verified — the link proved control of it.
        await users.UpdatePasswordAndRotateStampAsync(userId.Value, hasher.Hash(request.Password), ct);

        logger.LogInformation("Password reset completed. UserId={UserId}", userId);
        return Results.NoContent();
    }
}
