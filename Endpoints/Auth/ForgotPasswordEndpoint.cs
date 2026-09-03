using claude_starter.Repositories;
using claude_starter.Services.Auth;

namespace claude_starter.Endpoints.Auth;

public static class ForgotPasswordEndpoint
{
    public sealed record ForgotPasswordRequest(string Email);

    public static IEndpointRouteBuilder MapForgotPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/forgot-password", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        ForgotPasswordRequest request,
        HttpContext http,
        IUserRepository users,
        EmailLinkService links,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.ForgotPassword");
        var email = (request.Email ?? string.Empty).Trim();

        var user = string.IsNullOrEmpty(email) ? null : await users.GetByEmailAsync(email, ct);

        if (user is not null && user.IsActive)
        {
            await links.SendPasswordResetAsync(user, http.Request, ct);
            logger.LogInformation("Password reset requested. UserId={UserId}", user.Id);
        }
        else
        {
            logger.LogInformation("Password reset requested for an unknown or inactive address.");
        }

        // Always the same answer. Distinguishing the cases here would turn this
        // endpoint into a way to test whether an address has an account.
        return Results.Accepted();
    }
}
