namespace claude_starter.Models;

/// <summary>
/// A single-use link sent by email. Only the hash of the token is stored.
/// </summary>
public sealed class UserToken
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime? ConsumedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public static class TokenPurpose
{
    public const string EmailVerification = "email_verification";
    public const string PasswordReset = "password_reset";
}
