using claude_starter.Migrations;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace claude_starter.IntegrationTests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("claude_starter")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        DbMigrator.Apply(ConnectionString);

        await using var conn = new NpgsqlConnection(ConnectionString);
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

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner!.ResetAsync(conn);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
