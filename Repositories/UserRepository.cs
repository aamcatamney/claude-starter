using Dapper;
using claude_starter.Data;
using claude_starter.Models;

namespace claude_starter.Repositories;

public sealed class UserRepository : IUserRepository
{
    private const string SelectColumns =
        "id, email, password_hash, display_name, is_active, email_verified, security_stamp, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public UserRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM users WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM users WHERE lower(email) = lower(@email)",
                new { email },
                cancellationToken: ct));
    }

    public async Task<Guid> CreateAsync(string email, string passwordHash, string? displayName, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO users (email, password_hash, display_name)
                  VALUES (lower(@email), @passwordHash, @displayName)
                  RETURNING id",
                new { email, passwordHash, displayName },
                cancellationToken: ct));
    }

    public async Task<bool> UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE users SET password_hash = @passwordHash, updated_at = now()
                  WHERE id = @id",
                new { id, passwordHash },
                cancellationToken: ct));
        return rows > 0;
    }

    /// <summary>
    /// Rotates the security stamp alongside the password, so cookies issued
    /// before the reset stop validating. Someone resetting a password may be
    /// evicting an intruder; leaving their session alive defeats the exercise.
    /// </summary>
    public async Task<bool> UpdatePasswordAndRotateStampAsync(Guid id, string passwordHash, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE users
                  SET password_hash = @passwordHash,
                      security_stamp = gen_random_uuid(),
                      email_verified = true,
                      updated_at = now()
                  WHERE id = @id",
                new { id, passwordHash },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetEmailVerifiedAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE users SET email_verified = true, updated_at = now()
                  WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE users SET is_active = @isActive, updated_at = now()
                  WHERE id = @id",
                new { id, isActive },
                cancellationToken: ct));
        return rows > 0;
    }
}
