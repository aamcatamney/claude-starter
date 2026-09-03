using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace claude_starter.Endpoints.Auth;

public static class LogoutEndpoint
{
    public static IEndpointRouteBuilder MapLogoutEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/logout", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        HttpContext http,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Auth.Logout");

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Logout is deliberately not gated on the antiforgery token. Rejecting
        // the request would leave the auth cookie in place while the client
        // cleared its own state, so the user would look signed out with a live
        // session behind them. The exposure that buys back is a forced logout
        // via CSRF: an annoyance, not a compromise. A failure is logged so a
        // burst of them is still visible.
        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(
                "Logout antiforgery validation failed; signing out regardless. UserId={UserId} Reason={Reason}",
                userId,
                ex.Message);
        }

        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        logger.LogInformation("Logout. UserId={UserId}", userId);
        return Results.NoContent();
    }
}
