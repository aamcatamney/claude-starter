using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using claude_starter.Models;
using claude_starter.Repositories;
using claude_starter.Services.Diagnostics;
using claude_starter.Services.Email;

namespace claude_starter.Services.Auth;

/// <summary>
/// Mints, sends and redeems the single-use links used for email verification
/// and password reset.
/// </summary>
public sealed class EmailLinkService
{
    public static readonly TimeSpan VerificationLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    private readonly IUserTokenRepository _tokens;
    private readonly IEmailSender _email;
    private readonly AppMetrics _metrics;
    private readonly AuthOptions _auth;

    public EmailLinkService(
        IUserTokenRepository tokens,
        IEmailSender email,
        AppMetrics metrics,
        IOptions<AuthOptions> auth)
    {
        _tokens = tokens;
        _email = email;
        _metrics = metrics;
        _auth = auth.Value;
    }

    /// <summary>
    /// SHA-256 is right here and BCrypt is not: these tokens are 256 bits of
    /// randomness, so there is nothing to brute-force and a slow hash would
    /// only cost latency on every redemption.
    /// </summary>
    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string NewToken() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task SendVerificationAsync(User user, HttpRequest request, CancellationToken ct = default)
    {
        // Issuing a new link retires any outstanding one, so the most recent
        // mail is always the one that works.
        await _tokens.InvalidateOutstandingAsync(user.Id, TokenPurpose.EmailVerification, ct);

        var token = NewToken();
        await _tokens.CreateAsync(
            user.Id,
            TokenPurpose.EmailVerification,
            HashToken(token),
            DateTimeOffset.UtcNow.Add(VerificationLifetime),
            ct);

        var link = $"{BaseUrl(request)}/verify-email?token={Uri.EscapeDataString(token)}";

        await _email.SendAsync(
            user.Email,
            "Confirm your email address",
            $"""
             Confirm your email address by opening this link:

             {link}

             The link works once and expires in {VerificationLifetime.TotalHours:0} hours.

             If you did not create an account, ignore this message.
             """,
            ct);

        _metrics.EmailSent(TokenPurpose.EmailVerification);
    }

    public async Task SendPasswordResetAsync(User user, HttpRequest request, CancellationToken ct = default)
    {
        await _tokens.InvalidateOutstandingAsync(user.Id, TokenPurpose.PasswordReset, ct);

        var token = NewToken();
        await _tokens.CreateAsync(
            user.Id,
            TokenPurpose.PasswordReset,
            HashToken(token),
            DateTimeOffset.UtcNow.Add(ResetLifetime),
            ct);

        var link = $"{BaseUrl(request)}/reset-password?token={Uri.EscapeDataString(token)}";

        await _email.SendAsync(
            user.Email,
            "Reset your password",
            $"""
             Reset your password by opening this link:

             {link}

             The link works once and expires in {ResetLifetime.TotalMinutes:0} minutes.

             If you did not ask to reset your password, ignore this message —
             your password has not changed.
             """,
            ct);

        _metrics.EmailSent(TokenPurpose.PasswordReset);
    }

    /// <summary>
    /// Redeems a token, returning the user id it belonged to, or null when the
    /// token is unknown, already used or expired.
    /// </summary>
    public async Task<Guid?> RedeemAsync(string purpose, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _metrics.TokenRedemption(purpose, succeeded: false);
            return null;
        }

        var stored = await _tokens.GetUsableAsync(purpose, HashToken(token), ct);
        if (stored is null)
        {
            _metrics.TokenRedemption(purpose, succeeded: false);
            return null;
        }

        // Losing this race means another request redeemed the same link first.
        var consumed = await _tokens.ConsumeAsync(stored.Id, ct);
        _metrics.TokenRedemption(purpose, consumed);
        return consumed ? stored.UserId : null;
    }

    private string BaseUrl(HttpRequest request) =>
        string.IsNullOrWhiteSpace(_auth.AppBaseUrl)
            ? $"{request.Scheme}://{request.Host}"
            : _auth.AppBaseUrl.TrimEnd('/');
}
