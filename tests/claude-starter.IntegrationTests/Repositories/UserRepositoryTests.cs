using claude_starter.Data;
using claude_starter.IntegrationTests.Infrastructure;
using claude_starter.Repositories;
using Microsoft.Extensions.Configuration;

namespace claude_starter.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private UserRepository _repo = null!;

    public UserRepositoryTests(PostgresFixture fixture)
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
        _repo = new UserRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateAsync_StoresUser_AndReturnsId()
    {
        var id = await _repo.CreateAsync("alice@example.com", "hash", "Alice");

        id.Should().NotBe(Guid.Empty);
        var fetched = await _repo.GetByIdAsync(id);
        fetched.Should().NotBeNull();
        fetched!.Email.Should().Be("alice@example.com");
        fetched.DisplayName.Should().Be("Alice");
        fetched.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByEmailAsync_IsCaseInsensitive()
    {
        await _repo.CreateAsync("BOB@example.com", "hash", null);

        var lower = await _repo.GetByEmailAsync("bob@example.com");
        var mixed = await _repo.GetByEmailAsync("Bob@Example.com");

        lower.Should().NotBeNull();
        mixed.Should().NotBeNull();
        lower!.Id.Should().Be(mixed!.Id);
    }

    [Fact]
    public async Task GetByEmailAsync_UnknownEmail_ReturnsNull()
    {
        var user = await _repo.GetByEmailAsync("nobody@example.com");
        user.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var user = await _repo.GetByIdAsync(Guid.NewGuid());
        user.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePasswordAsync_KnownUser_ReturnsTrue_AndPersists()
    {
        var id = await _repo.CreateAsync("carol@example.com", "old-hash", null);

        var updated = await _repo.UpdatePasswordAsync(id, "new-hash");

        updated.Should().BeTrue();
        var fetched = await _repo.GetByIdAsync(id);
        fetched!.PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public async Task UpdatePasswordAsync_UnknownUser_ReturnsFalse()
    {
        var updated = await _repo.UpdatePasswordAsync(Guid.NewGuid(), "new-hash");
        updated.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveAsync_FlipsFlag()
    {
        var id = await _repo.CreateAsync("dave@example.com", "hash", null);

        var updated = await _repo.SetActiveAsync(id, false);

        updated.Should().BeTrue();
        var fetched = await _repo.GetByIdAsync(id);
        fetched!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_Throws()
    {
        await _repo.CreateAsync("eve@example.com", "hash", null);

        var act = () => _repo.CreateAsync("EVE@example.com", "hash", null);

        await act.Should().ThrowAsync<Exception>();
    }
}
