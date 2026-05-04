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

        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid antiforgery token");
        }

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);

        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        logger.LogInformation("Logout. UserId={UserId}", userId);
        return Results.NoContent();
    }
}
