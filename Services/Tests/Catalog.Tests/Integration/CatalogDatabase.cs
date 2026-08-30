using AutoMapper;
using Catalog.Common.Data;
using Catalog.Common.Mappings;
using Catalog.Common.Repositories;
using Catalog.GRPC.Mappings;
using Eventus.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Tests.Integration;

[CollectionDefinition(Name)]
public class CatalogDatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "catalog-postgres";
}

/// <summary>
/// Base for tests that need a real Catalog schema. Each class gets its own database, built by
/// running the service's actual migrations, so the tests exercise the same DDL production does
/// - including the <c>numeric</c> columns that carry every pool and price.
/// </summary>
[Collection(CatalogDatabaseCollection.Name)]
public abstract class CatalogDatabaseTest(PostgresFixture postgres) : IAsyncLifetime
{
    private string _connectionString = null!;

    protected EventContext Context { get; private set; } = null!;
    protected IEventRepository Repository { get; private set; } = null!;

    /// <summary>Both Catalog profiles, matching what the gRPC host composes at startup.</summary>
    protected IMapper Mapper { get; } =
        new MapperConfiguration(
            cfg =>
            {
                cfg.AddProfile<EventMappingProfile>();
                cfg.AddProfile<CatalogGrpcMappingProfile>();
            },
            NullLoggerFactory.Instance
        ).CreateMapper();

    public async Task InitializeAsync()
    {
        _connectionString = await postgres.CreateDatabaseAsync(GetType().Name);
        Context = NewContext();
        await Context.Database.MigrateAsync();
        Repository = new EventRepository(Context, Mapper);
    }

    /// <summary>
    /// A context with an empty change tracker. Assertions read through one of these so they see
    /// what actually reached Postgres rather than an entity still cached in memory.
    /// </summary>
    protected EventContext NewContext() =>
        new(new DbContextOptionsBuilder<EventContext>().UseNpgsql(_connectionString).Options);

    public async Task DisposeAsync() => await Context.DisposeAsync();
}
