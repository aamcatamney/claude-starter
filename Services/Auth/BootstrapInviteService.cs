using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using claude_starter.Services.Email;

namespace claude_starter.Services.Auth;

/// <summary>
/// Issues the invite that creates the first administrator.
///
/// The token is signed rather than stored: the guard that matters is that the
/// users table is empty, and creating the first account closes the door for
/// good. Nothing to migrate, nothing to clean up.
/// </summary>
public sealed class BootstrapInviteService
{
    private const string Purpose = "claude-starter.bootstrap-invite";

    /// <summary>
    /// Generous, because a fresh one is issued on every start while the
    /// deployment has no accounts.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly AuthOptions _auth;

    public BootstrapInviteService(IDataProtectionProvider provider, IOptions<AuthOptions> auth)
    {
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
        _auth = auth.Value;
    }

    public string Issue() => _protector.Protect("bootstrap", Lifetime);

    /// <summary>
    /// Whether a token is a genuine, unexpired invite. Says nothing about
    /// whether an account already exists — the caller checks that, because it
    /// is the condition that actually closes registration.
    /// </summary>
    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            return _protector.Unprotect(token) == "bootstrap";
        }
        catch (Exception)
        {
            // Tampered with, expired, or protected by keys this instance no
            // longer holds.
            return false;
        }
    }

    public string BuildLink(string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_auth.AppBaseUrl)
            ? "http://localhost:5000"
            : _auth.AppBaseUrl.TrimEnd('/');

        return $"{baseUrl}/register?token={Uri.EscapeDataString(token)}";
    }
}
