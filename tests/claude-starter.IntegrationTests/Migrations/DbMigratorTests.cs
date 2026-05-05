using claude_starter.IntegrationTests.Infrastructure;
using claude_starter.Migrations;
using Dapper;
using Npgsql;

namespace claude_starter.IntegrationTests.Migrations;

[Collection(IntegrationTestCollection.Name)]
public sealed class DbMigratorTests
{
    private readonly PostgresFixture _fixture;

    public DbMigratorTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Apply_OnAlreadyMigratedDb_IsIdempotent()
    {
        DbMigrator.Apply(_fixture.ConnectionString);
        DbMigrator.Apply(_fixture.ConnectionString);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        var tables = (await conn.QueryAsync<string>(
            @"SELECT table_name FROM information_schema.tables
              WHERE table_schema = 'public' ORDER BY table_name")).ToList();

        tables.Should().Contain("users");
        tables.Should().Contain("data_protection_keys");
        tables.Should().Contain("schemaversions");
    }

    [Fact]
    public async Task Apply_RecordsExecutedScripts()
    {
        DbMigrator.Apply(_fixture.ConnectionString);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        var scripts = (await conn.QueryAsync<string>(
            "SELECT scriptname FROM schemaversions ORDER BY scriptname")).ToList();

        scripts.Should().Contain(s => s.EndsWith("0001_create_users.sql"));
        scripts.Should().Contain(s => s.EndsWith("0002_data_protection_keys.sql"));
    }
}
