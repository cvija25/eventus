using Catalog.Common.Extensions;
using Eventus.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Catalog.Tests.Integration;

/// <summary>
/// What the services actually create on startup. Catalog spans two DbContexts over one database,
/// so "the schema is migrated" is no longer a single call - and a context left out is invisible
/// until the first write to it fails in production.
/// </summary>
[Collection(CatalogDatabaseCollection.Name)]
public class CatalogMigrationTests(PostgresFixture postgres)
{
    /// <summary>
    /// Drives the real MigrateCatalogDatabase over a real host. Reimplementing what it does would
    /// let the copy drift from the original, which is exactly the bug being guarded against.
    /// </summary>
    private static async Task MigrateAsStartupDoes(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CatalogDb"] = connectionString,
                }
            )
            .Build();

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddCatalogCommon(configuration);
            })
            .Build();

        await host.MigrateCatalogDatabase();
    }

    private static async Task<bool> TableExists(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"select to_regclass('public.\"{table}\"') is not null",
            connection
        );
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Startup_migrates_every_catalog_context()
    {
        var connectionString = await postgres.CreateDatabaseAsync(nameof(CatalogMigrationTests));

        await MigrateAsStartupDoes(connectionString);

        Assert.True(await TableExists(connectionString, "Events"), "Events table was not created");
        // Written by the Kafka price consumer on the trade path, so a missing table takes the
        // catalog service down rather than merely losing a chart.
        Assert.True(
            await TableExists(connectionString, "Histories"),
            "Histories table was not created"
        );
    }

    [Fact]
    public async Task Startup_migration_is_safe_to_run_again()
    {
        var connectionString = await postgres.CreateDatabaseAsync(nameof(CatalogMigrationTests));

        await MigrateAsStartupDoes(connectionString);
        await MigrateAsStartupDoes(connectionString);

        // Catalog.API and Catalog.GRPC both migrate the same database on startup.
        Assert.True(await TableExists(connectionString, "Histories"));
    }
}
