using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace claude_starter.Services.Passkeys;

/// <summary>
/// Holds the challenge between issuing options and verifying the response.
///
/// It lives in an encrypted cookie rather than a table: the Data Protection
/// keys are already shared through Postgres, so this works across instances
/// with no schema and nothing to clean up, and it binds the challenge to the
/// browser that asked for it.
/// </summary>
public sealed class PasskeyChallengeStore
{
    private const string RegistrationCookie = "passkey-registration";
    private const string SignInCookie = "passkey-signin";

    /// <summary>
    /// Long enough for someone to find their phone, short enough that a
    /// challenge cannot be hoarded.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IDataProtector _protector;

    public PasskeyChallengeStore(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("claude-starter.passkeys.challenge");
    }

    private sealed record Envelope(string Options, DateTimeOffset ExpiresAt);

    public void StashRegistration(HttpContext http, string optionsJson) =>
        Stash(http, RegistrationCookie, optionsJson);

    public void StashSignIn(HttpContext http, string optionsJson) =>
        Stash(http, SignInCookie, optionsJson);

    public string? TakeRegistration(HttpContext http) => Take(http, RegistrationCookie);

    public string? TakeSignIn(HttpContext http) => Take(http, SignInCookie);

    private void Stash(HttpContext http, string name, string optionsJson)
    {
        var payload = JsonSerializer.Serialize(
            new Envelope(optionsJson, DateTimeOffset.UtcNow.Add(Lifetime)));

        http.Response.Cookies.Append(name, _protector.Protect(payload), new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = Lifetime,
        });
    }

    /// <summary>
    /// Reads the challenge and deletes the cookie, so one set of options can
    /// only complete one ceremony.
    /// </summary>
    private string? Take(HttpContext http, string name)
    {
        if (!http.Request.Cookies.TryGetValue(name, out var protectedValue))
        {
            return null;
        }

        http.Response.Cookies.Delete(name);

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(_protector.Unprotect(protectedValue));
            if (envelope is null || envelope.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return null;
            }

            return envelope.Options;
        }
        catch (Exception)
        {
            // Tampered with, or encrypted under a key that has since been
            // retired. Either way there is no usable challenge here.
            return null;
        }
    }
}
