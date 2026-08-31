using Testcontainers.MongoDb;
using Xunit;

namespace Eventus.Testing;

/// <summary>A MongoDB container shared across a collection; see <see cref="PostgresFixture"/>.</summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private MongoDbContainer? _container;

    private MongoDbContainer Container =>
        _container ?? throw new InvalidOperationException("Fixture not initialised.");

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync()
    {
        ContainerEnvironment.Ensure();
        _container = new MongoDbBuilder("mongo:8").Build();
        return _container.StartAsync();
    }

    public Task DisposeAsync() => _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
}
