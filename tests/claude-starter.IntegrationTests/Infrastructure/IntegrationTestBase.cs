using System.Net.Http;

namespace claude_starter.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; }
    protected HttpClient Client { get; private set; } = null!;
    protected TestDataSeeder Seeder => Fixture.Seeder;

    protected IntegrationTestBase(DatabaseFixture fixture)
    {
        Fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await Fixture.ResetAsync();
        // A fresh client per test, so cookies never leak between cases. The
        // application behind it is shared for the whole collection.
        Client = Fixture.Factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
