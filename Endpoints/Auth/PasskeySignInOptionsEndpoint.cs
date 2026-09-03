using Fido2NetLib;
using Fido2NetLib.Objects;
using claude_starter.Services.Passkeys;

namespace claude_starter.Endpoints.Auth;

/// <summary>
/// Step one of signing in with a passkey. No email is asked for and none is
/// returned: the credential list is empty, so the authenticator offers whatever
/// it holds for this site and the account falls out of the response.
/// </summary>
public static class PasskeySignInOptionsEndpoint
{
    public static IEndpointRouteBuilder MapPasskeySignInOptionsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/passkeys/sign-in-options", Handle);
        return app;
    }

    private static IResult Handle(HttpContext http, IFido2 fido2, PasskeyChallengeStore challenges)
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Preferred,
        });

        challenges.StashSignIn(http, options.ToJson());

        return Results.Content(options.ToJson(), "application/json");
    }
}
