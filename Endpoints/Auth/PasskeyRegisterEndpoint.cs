using System.Security.Claims;
using Fido2NetLib;
using claude_starter.Repositories;
using claude_starter.Services.Passkeys;

namespace claude_starter.Endpoints.Auth;

/// <summary>
/// Step two: verify what the authenticator produced and store the credential.
/// </summary>
public static class PasskeyRegisterEndpoint
{
    public sealed record RegisterPasskeyRequest(AuthenticatorAttestationRawResponse Response, string? Name);

    public static IEndpointRouteBuilder MapPasskeyRegisterEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/passkeys/register", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        RegisterPasskeyRequest request,
        HttpContext http,
        IFido2 fido2,
        IPasskeyRepository passkeys,
        PasskeyChallengeStore challenges,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.PasskeyRegister");

        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

        var stashed = challenges.TakeRegistration(http);
        if (stashed is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "No registration in progress",
                detail: "Start again — the challenge expired or was already used.");
        }

        try
        {
            var credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = request.Response,
                OriginalOptions = CredentialCreateOptions.FromJson(stashed),
                IsCredentialIdUniqueToUserCallback = async (args, cancellation) =>
                    !await passkeys.ExistsAsync(args.CredentialId, cancellation),
            }, ct);

            var name = string.IsNullOrWhiteSpace(request.Name) ? "Passkey" : request.Name.Trim();

            var id = await passkeys.CreateAsync(
                userId,
                credential.Id,
                credential.PublicKey,
                credential.SignCount,
                credential.AaGuid,
                credential.Transports is { Length: > 0 }
                    ? string.Join(',', credential.Transports)
                    : null,
                name,
                ct);

            logger.LogInformation("Passkey registered. UserId={UserId} PasskeyId={PasskeyId}", userId, id);

            return Results.Ok(new { id, name });
        }
        catch (Fido2VerificationException ex)
        {
            logger.LogWarning("Passkey registration rejected. UserId={UserId} Reason={Reason}", userId, ex.Message);
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Could not register that passkey",
                detail: ex.Message);
        }
    }
}
