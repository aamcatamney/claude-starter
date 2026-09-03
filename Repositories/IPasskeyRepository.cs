using claude_starter.Models;

namespace claude_starter.Repositories;

public interface IPasskeyRepository
{
    Task<Guid> CreateAsync(
        Guid userId,
        byte[] credentialId,
        byte[] publicKey,
        long signCount,
        Guid? aaguid,
        string? transports,
        string name,
        CancellationToken ct = default);

    /// <summary>
    /// Looked up with no user in hand: a discoverable credential identifies the
    /// account, rather than the account identifying the credential.
    /// </summary>
    Task<Passkey?> GetByCredentialIdAsync(byte[] credentialId, CancellationToken ct = default);

    Task<IReadOnlyList<Passkey>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Records a successful assertion: the new counter and the time.</summary>
    Task UpdateOnUseAsync(Guid id, long signCount, CancellationToken ct = default);

    /// <summary>Scoped by user so one account cannot delete another's key.</summary>
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<bool> ExistsAsync(byte[] credentialId, CancellationToken ct = default);
}
