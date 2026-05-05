using System.Net.Http;

namespace claude_starter.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; }
    protected TestWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected TestDataSeeder Seeder { get; }

    protected IntegrationTestBase(PostgresFixture fixture)
    {
        Fixture = fixture;
        Seeder = new TestDataSeeder(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await Fixture.ResetAsync();
        Factory = new TestWebApplicationFactory(Fixture.ConnectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }
}
