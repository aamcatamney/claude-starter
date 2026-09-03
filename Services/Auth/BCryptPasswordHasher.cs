using Microsoft.Extensions.Configuration;

namespace claude_starter.Services.Auth;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Cost of a single hash, as a power of two. Raise it as hardware gets
    /// faster; a hash should stay expensive enough to make guessing painful.
    /// </summary>
    public const int DefaultWorkFactor = 12;

    private readonly int _workFactor;

    public BCryptPasswordHasher(IConfiguration configuration)
        : this(configuration.GetValue<int?>("Auth:BCryptWorkFactor") ?? DefaultWorkFactor)
    {
    }

    // Tests drop the work factor to keep a suite that hashes constantly from
    // paying production cost on every case.
    public BCryptPasswordHasher(int workFactor)
    {
        _workFactor = workFactor;
    }

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, _workFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
