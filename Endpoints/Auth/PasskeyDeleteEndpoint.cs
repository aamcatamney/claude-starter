using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using claude_starter.Repositories;

namespace claude_starter.Endpoints.Auth;

public static class PasskeyDeleteEndpoint
{
    public static IEndpointRouteBuilder MapPasskeyDeleteEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/passkeys/{id:guid}", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        HttpContext http,
        IAntiforgery antiforgery,
        IPasskeyRepository passkeys,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.PasskeyDelete");

        // Unlike logout, refusing here is the safe outcome: the credential
        // simply stays, and the user can try again.
        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid antiforgery token");
        }

        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

        var removed = await passkeys.DeleteAsync(id, userId, ct);
        if (!removed)
        {
            // Either it never existed or it belongs to somebody else. The
            // answer is the same so the endpoint cannot be used to probe.
            return Results.NotFound();
        }

        logger.LogInformation("Passkey removed. UserId={UserId} PasskeyId={PasskeyId}", userId, id);
        return Results.NoContent();
    }
}
