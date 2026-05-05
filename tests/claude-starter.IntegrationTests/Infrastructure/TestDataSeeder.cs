using Dapper;
using claude_starter.Models;
using claude_starter.Services.Auth;
using Npgsql;

namespace claude_starter.IntegrationTests.Infrastructure;

public sealed class TestDataSeeder
{
    private readonly string _connectionString;
    private readonly IPasswordHasher _hasher = new BCryptPasswordHasher();

    public TestDataSeeder(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<User> CreateUserAsync(
        string email,
        string password,
        string? displayName = null,
        bool isActive = true)
    {
        var hash = _hasher.Hash(password);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<Guid>(
            @"INSERT INTO users (email, password_hash, display_name, is_active)
              VALUES (lower(@email), @hash, @displayName, @isActive)
              RETURNING id",
            new { email, hash, displayName, isActive });

        return new User
        {
            Id = id,
            Email = email.ToLowerInvariant(),
            PasswordHash = hash,
            DisplayName = displayName,
            IsActive = isActive,
        };
    }
}
