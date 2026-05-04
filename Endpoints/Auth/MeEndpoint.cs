using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using claude_starter.Repositories;

namespace claude_starter.Endpoints.Auth;

public static class MeEndpoint
{
    public static IEndpointRouteBuilder MapMeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        HttpContext http,
        IUserRepository users,
        IAntiforgery antiforgery,
        CancellationToken ct)
    {
        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var id))
        {
            return Results.Unauthorized();
        }

        var user = await users.GetByIdAsync(id, ct);
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        AuthEndpoints.IssueXsrfCookie(http, antiforgery);

        return Results.Ok(new LoginEndpoint.UserResponse(user.Id, user.Email, user.DisplayName));
    }
}
