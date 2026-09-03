using claude_starter.Data;
using claude_starter.IntegrationTests.Infrastructure;
using claude_starter.Models;
using claude_starter.Repositories;
using claude_starter.Services.Auth;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace claude_starter.IntegrationTests.Repositories;

[Collection(TokenRepositoryCollection.Name)]
public sealed class UserTokenRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private UserTokenRepository _repo = null!;

    public UserTokenRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _fixture.ConnectionString,
            })
            .Build();
        _repo = new UserTokenRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Guid> CreateUserAsync(string email) =>
        (await _fixture.Seeder.CreateUserAsync(email, "correct-horse-battery")).Id;

    /// <summary>
    /// Backdates a row, which is the only way to test retention without waiting
    /// thirty days.
    /// </summary>
    private async Task BackdateAsync(string tokenHash, TimeSpan age)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            @"UPDATE user_tokens
              SET expires_at = now() - @age,
                  consumed_at = CASE WHEN consumed_at IS NULL THEN NULL ELSE now() - @age END
              WHERE token_hash = @tokenHash",
            new { tokenHash, age });
    }

    private async Task<int> CountAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM user_tokens");
    }

    [Fact]
    public async Task DeleteDeadTokens_RemovesExpiredRowsPastRetention()
    {
        var userId = await CreateUserAsync("expired@example.com");
        const string hash = "hash-expired";
        await _repo.CreateAsync(userId, TokenPurpose.PasswordReset, hash, DateTimeOffset.UtcNow.AddHours(1));
        await BackdateAsync(hash, TimeSpan.FromDays(31));

        var deleted = await _repo.DeleteDeadTokensAsync(TimeSpan.FromDays(30));

        deleted.Should().Be(1);
        (await CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteDeadTokens_KeepsExpiredRowsInsideRetention()
    {
        var userId = await CreateUserAsync("recent@example.com");
        const string hash = "hash-recent";
        await _repo.CreateAsync(userId, TokenPurpose.PasswordReset, hash, DateTimeOffset.UtcNow.AddHours(1));
        await BackdateAsync(hash, TimeSpan.FromDays(2));

        var deleted = await _repo.DeleteDeadTokensAsync(TimeSpan.FromDays(30));

        // Dead, but still inside the window where it can answer questions.
        deleted.Should().Be(0);
        (await CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteDeadTokens_LeavesLiveTokensAlone()
    {
        var userId = await CreateUserAsync("live@example.com");
        await _repo.CreateAsync(userId, TokenPurpose.EmailVerification, "hash-live", DateTimeOffset.UtcNow.AddHours(24));

        var deleted = await _repo.DeleteDeadTokensAsync(TimeSpan.FromDays(30));

        deleted.Should().Be(0);
        (await _repo.GetUsableAsync(TokenPurpose.EmailVerification, "hash-live")).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteDeadTokens_RemovesConsumedRowsPastRetention()
    {
        var userId = await CreateUserAsync("consumed@example.com");
        const string hash = "hash-consumed";
        await _repo.CreateAsync(userId, TokenPurpose.PasswordReset, hash, DateTimeOffset.UtcNow.AddHours(1));

        var stored = await _repo.GetUsableAsync(TokenPurpose.PasswordReset, hash);
        await _repo.ConsumeAsync(stored!.Id);
        await BackdateAsync(hash, TimeSpan.FromDays(31));

        var deleted = await _repo.DeleteDeadTokensAsync(TimeSpan.FromDays(30));

        deleted.Should().Be(1);
        (await CountAsync()).Should().Be(0);
    }
}
