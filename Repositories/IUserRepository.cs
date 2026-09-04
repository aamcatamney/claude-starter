using claude_starter.Models;

namespace claude_starter.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Guid> CreateAsync(
        string email,
        string passwordHash,
        string? displayName,
        bool isAdmin = false,
        CancellationToken ct = default);

    /// <summary>Whether any account exists. Gates the bootstrap invite.</summary>
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<bool> UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default);

    /// <summary>
    /// Sets the password, rotates the security stamp so existing cookies stop
    /// validating, and marks the email verified — reaching this means the user
    /// followed a link sent to that address.
    /// </summary>
    Task<bool> UpdatePasswordAndRotateStampAsync(Guid id, string passwordHash, CancellationToken ct = default);

    Task<bool> SetEmailVerifiedAsync(Guid id, CancellationToken ct = default);
    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
}
