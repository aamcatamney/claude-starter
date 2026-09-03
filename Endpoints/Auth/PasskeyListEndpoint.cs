using System.Security.Claims;
using claude_starter.Repositories;

namespace claude_starter.Endpoints.Auth;

public static class PasskeyListEndpoint
{
    public sealed record PasskeySummary(Guid Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt);

    public static IEndpointRouteBuilder MapPasskeyListEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/passkeys", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        HttpContext http,
        IPasskeyRepository passkeys,
        CancellationToken ct)
    {
        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

        var rows = await passkeys.ListForUserAsync(userId, ct);

        // Credential ids and public keys stay on the server. The client only
        // needs enough to show a list and offer a delete.
        return Results.Ok(rows
            .Select(p => new PasskeySummary(p.Id, p.Name, p.CreatedAt, p.LastUsedAt))
            .ToList());
    }
}
