using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Eventus.Testing;

/// <summary>
/// One Postgres container shared by every test class in a collection. Container startup is
/// the expensive part, so it is paid once; isolation comes from handing each class its own
/// freshly created database via <see cref="CreateDatabaseAsync"/>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    private PostgreSqlContainer Container =>
        _container ?? throw new InvalidOperationException("Fixture not initialised.");

    public Task InitializeAsync()
    {
        ContainerEnvironment.Ensure();
        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        return _container.StartAsync();
    }

    public Task DisposeAsync() => _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;

    /// <summary>Creates an empty database and returns a connection string pointing at it.</summary>
    public async Task<string> CreateDatabaseAsync(string name)
    {
        var database = UniqueDatabaseName(name);

        await using (var connection = new NpgsqlConnection(Container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"CREATE DATABASE \"{database}\"",
                connection
            );
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
        {
            Database = database,
        }.ToString();
    }

    private static string UniqueDatabaseName(string name)
    {
        var cleaned = new string(
            name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray()
        );
        var suffix = Guid.NewGuid().ToString("N");
        // Postgres truncates identifiers at 63 bytes; keep the random suffix intact so
        // names stay unique even when the prefix is long.
        var prefix = cleaned[..Math.Min(cleaned.Length, 62 - suffix.Length)];
        return $"{prefix}_{suffix}";
    }
}
