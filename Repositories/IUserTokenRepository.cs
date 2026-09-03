using claude_starter.Models;

namespace claude_starter.Repositories;

public interface IUserTokenRepository
{
    Task CreateAsync(Guid userId, string purpose, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default);

    /// <summary>
    /// Returns the token only while it is unconsumed and unexpired.
    /// </summary>
    Task<UserToken?> GetUsableAsync(string purpose, string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Marks a token consumed. Returns false if something already did, which is
    /// what makes a link single-use under concurrent requests.
    /// </summary>
    Task<bool> ConsumeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Consumes every outstanding token of a purpose, so issuing a new link
    /// retires the old ones.
    /// </summary>
    Task InvalidateOutstandingAsync(Guid userId, string purpose, CancellationToken ct = default);

    /// <summary>
    /// Deletes tokens that have been spent or have expired, once they are older
    /// than <paramref name="retention"/>. Returns how many rows went.
    /// </summary>
    Task<int> DeleteDeadTokensAsync(TimeSpan retention, CancellationToken ct = default);
}
