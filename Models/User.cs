namespace claude_starter.Models;

public sealed class User
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public bool IsActive { get; init; }
    public bool EmailVerified { get; init; }
    public Guid SecurityStamp { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
