using Dapper;
using claude_starter.Data;
using claude_starter.Models;

namespace claude_starter.Repositories;

public sealed class UserTokenRepository : IUserTokenRepository
{
    private readonly IDbConnectionFactory _factory;

    public UserTokenRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task CreateAsync(Guid userId, string purpose, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO user_tokens (user_id, purpose, token_hash, expires_at)
                  VALUES (@userId, @purpose, @tokenHash, @expiresAt)",
                new { userId, purpose, tokenHash, expiresAt = expiresAt.UtcDateTime },
                cancellationToken: ct));
    }

    public async Task<UserToken?> GetUsableAsync(string purpose, string tokenHash, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<UserToken>(
            new CommandDefinition(
                @"SELECT id, user_id, purpose, token_hash, expires_at, consumed_at, created_at
                  FROM user_tokens
                  WHERE purpose = @purpose
                    AND token_hash = @tokenHash
                    AND consumed_at IS NULL
                    AND expires_at > now()",
                new { purpose, tokenHash },
                cancellationToken: ct));
    }

    public async Task<bool> ConsumeAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // The consumed_at IS NULL predicate is the single-use guarantee: two
        // concurrent requests race here and exactly one updates a row.
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE user_tokens SET consumed_at = now() WHERE id = @id AND consumed_at IS NULL",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task InvalidateOutstandingAsync(Guid userId, string purpose, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE user_tokens SET consumed_at = now()
                  WHERE user_id = @userId AND purpose = @purpose AND consumed_at IS NULL",
                new { userId, purpose },
                cancellationToken: ct));
    }
}
