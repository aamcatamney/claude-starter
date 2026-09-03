using Microsoft.Extensions.Options;
using claude_starter.Services.Passkeys;

namespace claude_starter.Endpoints;

/// <summary>
/// What the client needs to know before anyone signs in. Only feature flags —
/// whether a button exists is not a secret, and the client would reveal it by
/// rendering anyway.
/// </summary>
public static class ConfigEndpoint
{
    public sealed record ClientConfig(bool PasskeysEnabled);

    public static IEndpointRouteBuilder MapConfigEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/config", (IOptions<PasskeyOptions> passkeys) =>
            Results.Ok(new ClientConfig(passkeys.Value.Enabled)));
        return app;
    }
}
