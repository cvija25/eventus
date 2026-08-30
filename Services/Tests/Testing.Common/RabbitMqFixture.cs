using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Testcontainers.RabbitMq;
using Xunit;

namespace Eventus.Testing;

/// <summary>
/// A real RabbitMQ broker. The services connect through <c>RabbitMqOptions</c>, so the
/// host and port are exposed individually for configuration overrides.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    private RabbitMqContainer Container =>
        _container ?? throw new InvalidOperationException("Fixture not initialised.");

    public const string UserName = "guest";
    public const string Password = "guest";

    public string HostName => Container.Hostname;
    public int Port => Container.GetMappedPublicPort(5672);
    public int ManagementPort => Container.GetMappedPublicPort(15672);

    /// <summary>
    /// Force-closes every client connection from the broker side, the way a broker restart or a
    /// network blip would. Lets tests exercise what the services do when the link drops under
    /// them rather than when they close it themselves.
    /// </summary>
    public async Task DropAllConnectionsAsync()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{HostName}:{ManagementPort}"),
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UserName}:{Password}"))
        );

        var listed = await client.GetFromJsonAsync<List<ConnectionInfo>>("/api/connections") ?? [];
        foreach (var connection in listed)
            await client.DeleteAsync($"/api/connections/{Uri.EscapeDataString(connection.Name)}");
    }

    private sealed record ConnectionInfo(string Name);

    public Task InitializeAsync()
    {
        ContainerEnvironment.Ensure();
        _container = new RabbitMqBuilder("rabbitmq:4-management-alpine")
            .WithUsername(UserName)
            .WithPassword(Password)
            .WithPortBinding(15672, true)
            .Build();
        return _container.StartAsync();
    }

    public Task DisposeAsync() => _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
}
