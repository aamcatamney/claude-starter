using Fido2NetLib;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using claude_starter.Repositories;
using claude_starter.Services.Diagnostics;
using claude_starter.Services.Email;
using claude_starter.Services.Passkeys;

namespace claude_starter.Endpoints.Auth;

/// <summary>
/// Step two: verify the assertion and establish a session.
/// </summary>
public static class PasskeySignInEndpoint
{
    public sealed record PasskeySignInRequest(AuthenticatorAssertionRawResponse Response, bool RememberMe);

    public static IEndpointRouteBuilder MapPasskeySignInEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/passkeys/sign-in", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        PasskeySignInRequest request,
        HttpContext http,
        IFido2 fido2,
        IUserRepository users,
        IPasskeyRepository passkeys,
        PasskeyChallengeStore challenges,
        IAntiforgery antiforgery,
        IOptions<AuthOptions> authOptions,
        AppMetrics metrics,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.PasskeySignIn");

        var stashed = challenges.TakeSignIn(http);
        if (stashed is null)
        {
            metrics.SignIn("invalid-credentials");
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "No sign-in in progress",
                detail: "Start again — the challenge expired or was already used.");
        }

        var stored = await passkeys.GetByCredentialIdAsync(request.Response.RawId, ct);
        if (stored is null)
        {
            // Unknown credential. Same answer as a bad password, for the same
            // reason: this must not reveal which passkeys the site knows.
            logger.LogWarning("Passkey sign-in with an unknown credential.");
            metrics.SignIn("invalid-credentials");
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var user = await users.GetByIdAsync(stored.UserId, ct);
        if (user is null || !user.IsActive)
        {
            metrics.SignIn("inactive");
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = request.Response,
                OriginalOptions = AssertionOptions.FromJson(stashed),
                StoredPublicKey = stored.PublicKey,
                StoredSignatureCounter = (uint)stored.SignCount,
                // The handle the authenticator returns must be the account the
                // credential is filed under, or a credential could be replayed
                // against someone else's row.
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(args.UserHandle.Length == 16 && new Guid(args.UserHandle) == stored.UserId),
            }, ct);

            await passkeys.UpdateOnUseAsync(stored.Id, result.SignCount, ct);

            if (authOptions.Value.RequireEmailVerification && !user.EmailVerified)
            {
                metrics.SignIn("unverified");
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Email not verified",
                    detail: "Confirm your email address before signing in.",
                    type: LoginEndpoint.EmailNotVerifiedType);
            }

            await AuthEndpoints.SignInAsync(http, user, request.RememberMe, antiforgery);

            logger.LogInformation("Passkey sign-in. UserId={UserId} PasskeyId={PasskeyId}", user.Id, stored.Id);
            metrics.SignIn("success");

            return Results.Ok(new LoginEndpoint.UserResponse(user.Id, user.Email, user.DisplayName, user.IsAdmin));
        }
        catch (Fido2VerificationException ex)
        {
            logger.LogWarning("Passkey assertion rejected. UserId={UserId} Reason={Reason}", stored.UserId, ex.Message);
            metrics.SignIn("invalid-credentials");
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }
    }
}
