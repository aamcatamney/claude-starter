using Dapper;
using claude_starter.Data;
using claude_starter.Models;

namespace claude_starter.Repositories;

public sealed class PasskeyRepository : IPasskeyRepository
{
    private const string SelectColumns =
        "id, user_id, credential_id, public_key, sign_count, aaguid, transports, name, created_at, last_used_at";

    private readonly IDbConnectionFactory _factory;

    public PasskeyRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Guid> CreateAsync(
        Guid userId,
        byte[] credentialId,
        byte[] publicKey,
        long signCount,
        Guid? aaguid,
        string? transports,
        string name,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO user_passkeys
                      (user_id, credential_id, public_key, sign_count, aaguid, transports, name)
                  VALUES (@userId, @credentialId, @publicKey, @signCount, @aaguid, @transports, @name)
                  RETURNING id",
                new { userId, credentialId, publicKey, signCount, aaguid, transports, name },
                cancellationToken: ct));
    }

    public async Task<Passkey?> GetByCredentialIdAsync(byte[] credentialId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Passkey>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM user_passkeys WHERE credential_id = @credentialId",
                new { credentialId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Passkey>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Passkey>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM user_passkeys WHERE user_id = @userId ORDER BY created_at",
                new { userId },
                cancellationToken: ct));
        return rows.ToList();
    }

    public async Task UpdateOnUseAsync(Guid id, long signCount, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE user_passkeys SET sign_count = @signCount, last_used_at = now() WHERE id = @id",
                new { id, signCount },
                cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // user_id is in the predicate, not checked beforehand: the delete is
        // then incapable of touching someone else's credential.
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM user_passkeys WHERE id = @id AND user_id = @userId",
                new { id, userId },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> ExistsAsync(byte[] credentialId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM user_passkeys WHERE credential_id = @credentialId)",
                new { credentialId },
                cancellationToken: ct));
    }
}
