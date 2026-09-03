namespace claude_starter.Models;

public sealed class Passkey
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public byte[] CredentialId { get; init; } = [];
    public byte[] PublicKey { get; init; } = [];
    public long SignCount { get; init; }
    public Guid? Aaguid { get; init; }
    public string? Transports { get; init; }

    /// <summary>What the user calls this authenticator in the list.</summary>
    public string Name { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
}
