using System.Security.Claims;
using Fido2NetLib;
using Fido2NetLib.Objects;
using claude_starter.Repositories;
using claude_starter.Services.Passkeys;

namespace claude_starter.Endpoints.Auth;

/// <summary>
/// Step one of adding a passkey: hand the browser a challenge. Requires a
/// session — a passkey is added to an account that already exists.
/// </summary>
public static class PasskeyRegisterOptionsEndpoint
{
    public static IEndpointRouteBuilder MapPasskeyRegisterOptionsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/passkeys/register-options", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        HttpContext http,
        IFido2 fido2,
        IUserRepository users,
        IPasskeyRepository passkeys,
        PasskeyChallengeStore challenges,
        CancellationToken ct)
    {
        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null) return Results.Unauthorized();

        var existing = await passkeys.ListForUserAsync(userId, ct);

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = userId.ToByteArray(),
                Name = user.Email,
                DisplayName = user.DisplayName ?? user.Email,
            },
            // Offering credentials the account already holds lets the
            // authenticator refuse to enrol the same device twice.
            ExcludeCredentials = existing
                .Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))
                .ToList(),
            AuthenticatorSelection = new AuthenticatorSelection
            {
                // Discoverable, so signing in later needs no email typed.
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Preferred,
            },
            // Nothing here inspects attestation statements, and asking for one
            // prompts the user about sharing device information for no gain.
            AttestationPreference = AttestationConveyancePreference.None,
        });

        challenges.StashRegistration(http, options.ToJson());

        return Results.Content(options.ToJson(), "application/json");
    }
}
