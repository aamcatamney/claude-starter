using claude_starter.Models;

namespace claude_starter.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Guid> CreateAsync(string email, string passwordHash, string? displayName, CancellationToken ct = default);
    Task<bool> UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default);
    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
}
