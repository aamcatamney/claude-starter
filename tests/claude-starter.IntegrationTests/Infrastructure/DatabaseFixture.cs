using claude_starter.Migrations;
using Npgsql;
using Respawn;

namespace claude_starter.IntegrationTests.Infrastructure;

/// <summary>
/// One database, one migrated schema and one running application per test
/// collection. Booting the app per test cost ~110ms each and re-ran the
/// migrator every time; collections are isolated by database instead.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly ContainerFixture _container;
    private Respawner? _respawner;

    public DatabaseFixture(ContainerFixture container)
    {
        _container = container;
    }

    public string ConnectionString { get; private set; } = null!;
    public TestWebApplicationFactory Factory { get; private set; } = null!;
    public TestDataSeeder Seeder { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await _container.CreateDatabaseAsync($"test_{Guid.NewGuid():N}");

        DbMigrator.Apply(ConnectionString);

        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore =
                [
                    new Respawn.Graph.Table("public", "schemaversions"),
                    new Respawn.Graph.Table("public", "data_protection_keys"),
                ],
            });
        }

        Factory = new TestWebApplicationFactory(ConnectionString);
        Seeder = new TestDataSeeder(ConnectionString);
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner!.ResetAsync(conn);
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }
}
