using Testcontainers.Kafka;
using Xunit;

namespace Eventus.Testing;

/// <summary>
/// A real Kafka broker. The price-update stream moved off RabbitMQ onto Kafka, so anything
/// exercising that flow needs this rather than <see cref="RabbitMqFixture"/>.
/// </summary>
public sealed class KafkaFixture : IAsyncLifetime
{
    private KafkaContainer? _container;

    private KafkaContainer Container =>
        _container ?? throw new InvalidOperationException("Fixture not initialised.");

    /// <summary>Value for <c>KafkaOptions.BootstrapServers</c>.</summary>
    public string BootstrapServers => Container.GetBootstrapAddress().Replace("PLAINTEXT://", "");

    public Task InitializeAsync()
    {
        ContainerEnvironment.Ensure();
        _container = new KafkaBuilder("confluentinc/cp-kafka:7.6.1").Build();
        return _container.StartAsync();
    }

    public Task DisposeAsync() => _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
}
