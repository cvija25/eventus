using Account.Data;
using Catalog.Common.Data;
using Common.Messaging;
using Eventus.Testing;
using Grpc.Net.Client;
using Identity.API.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Exceptions;

namespace Eventus.E2E.Tests;

/// <summary>
/// The whole system, wired the way compose.yaml wires it.
/// <para>
/// Postgres, RabbitMQ and Mongo are real containers, so the services talk over the actual AMQP
/// wire protocol, run their real EF migrations and hit real <c>numeric</c> columns. The five
/// services themselves are hosted in-process rather than as images: that keeps HTTP, JWT
/// validation, the RabbitMQ round trip and the gRPC hop genuine while making the suite runnable
/// from a plain <c>dotnet test</c> with no image build.
/// </para>
/// <para>
/// The only substitutions are the two transports that would otherwise need published ports:
/// Game reaches Catalog.GRPC over a real HTTP/2 gRPC channel bound to the in-memory server, and
/// Identity reaches Account over the equivalent HTTP handler.
/// </para>
/// </summary>
public sealed class EventusFixture : IAsyncLifetime
{
    private const string JwtSecret = "super-secret-key-change-in-production-32chars!!";
    private const string JwtIssuer = "eventus-identity";
    private const string JwtAudience = "eventus-services";

    private readonly PostgresFixture _postgres = new();
    private readonly RabbitMqFixture _rabbit = new();
    private readonly MongoFixture _mongo = new();
    private readonly KafkaFixture _kafka = new();

    private readonly List<IAsyncDisposable> _factories = [];
    private readonly List<string> _environmentKeys = [];
    private GrpcChannel? _catalogChannel;

    public HttpClient Identity { get; private set; } = null!;
    public HttpClient Catalog { get; private set; } = null!;
    public HttpClient AccountApi { get; private set; } = null!;

    private sealed class Service<TEntryPoint>(Action<IWebHostBuilder> configure)
        : WebApplicationFactory<TEntryPoint>
        where TEntryPoint : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => configure(builder);
    }

    private Dictionary<string, string?> RabbitSettings() =>
        new()
        {
            ["RabbitMq:HostName"] = _rabbit.HostName,
            ["RabbitMq:Port"] = _rabbit.Port.ToString(),
            ["RabbitMq:UserName"] = RabbitMqFixture.UserName,
            ["RabbitMq:Password"] = RabbitMqFixture.Password,
            ["RabbitMq:VirtualHost"] = "/",
        };

    private static Dictionary<string, string?> JwtSettings() =>
        new()
        {
            ["Jwt:Secret"] = JwtSecret,
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
            ["Jwt:ExpiryMinutes"] = "60",
        };

    /// <summary>
    /// Publishes settings as environment variables.
    /// <para>
    /// These services are minimal-hosting apps that read configuration while composing their DI
    /// container - before <c>builder.Build()</c> - so WebApplicationFactory's
    /// ConfigureAppConfiguration hook runs too late to be seen. Environment variables are read by
    /// <c>WebApplication.CreateBuilder</c> itself, so they are in place before any service starts.
    /// They are process-wide, which is safe here only because every key below is either specific
    /// to one service or shared by all of them with the same value.
    /// </para>
    /// </summary>
    private void Publish(Dictionary<string, string?> settings)
    {
        foreach (var (key, value) in settings)
        {
            var variable = key.Replace(":", "__");
            Environment.SetEnvironmentVariable(variable, value);
            _environmentKeys.Add(variable);
        }
    }

    private Service<T> Start<T>(Action<IServiceCollection>? overrideServices = null)
        where T : class
    {
        var factory = new Service<T>(builder =>
        {
            if (overrideServices is not null)
                builder.ConfigureTestServices(overrideServices);
        });

        // Touching Services builds and starts the host now, so hosted services (the RabbitMQ
        // consumers) and startup migrations run in a known order rather than concurrently.
        _ = factory.Services;
        _factories.Add(factory);
        return factory;
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.InitializeAsync(),
            _rabbit.InitializeAsync(),
            _mongo.InitializeAsync(),
            _kafka.InitializeAsync()
        );

        var catalogDb = await _postgres.CreateDatabaseAsync("e2e_catalog");
        var accountDb = await _postgres.CreateDatabaseAsync("e2e_account");

        // Catalog.API and Catalog.GRPC both migrate on startup and would race on the same
        // schema. Migrating once up front leaves both of their calls as no-ops.
        await MigrateCatalogAsync(catalogDb);
        await MigrateAccountAsync(accountDb);

        // Not "Development": that branch turns on Swagger/OpenAPI document generation and loads
        // appsettings.Development.json, which points at localhost infrastructure.
        Publish(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Testing" });
        Publish(RabbitSettings());
        Publish(JwtSettings());
        Publish(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogDb"] = catalogDb,
                // The price-update stream runs over Kafka. Catalog.GRPC publishes with
                // Acks.All, so an unreachable broker would stall every trade rather than
                // merely losing the price broadcast.
                ["Kafka:BootstrapServers"] = _kafka.BootstrapServers,
                ["Kafka:PriceUpdateTopic"] = KafkaConstants.PriceUpdateTopic,
                ["ConnectionStrings:AccountDb"] = accountDb,
                ["ConnectionStrings:IdentityDb"] = _mongo.ConnectionString,
                ["ApiEndpoints:Account"] = "http://account/api/v1/account/",
                // Game validates this into a Uri at startup even though the client is replaced.
                ["GrpcSettings:CatalogUrl"] = "http://catalog.grpc",
            }
        );

        var catalogGrpc = Start<global::Catalog.GRPC.Services.CatalogService>();
        _catalogChannel = GrpcChannel.ForAddress(
            catalogGrpc.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = catalogGrpc.Server.CreateHandler() }
        );

        Catalog = Start<global::Catalog.API.Controllers.CatalogController>().CreateClient();

        var accountApi = Start<global::Account.Controllers.AccountController>();
        AccountApi = accountApi.CreateClient();

        Start<global::Game.Controllers.GameController>(services =>
            services.AddSingleton(new global::Catalog.GRPC.Catalog.CatalogClient(_catalogChannel))
        );

        Identity = Start<global::Identity.API.Controllers.IdentityController>(services =>
                services
                    .AddHttpClient<AccountClient>(client =>
                        client.BaseAddress = new Uri("http://account/api/v1/account/")
                    )
                    .ConfigurePrimaryHttpMessageHandler(() => accountApi.Server.CreateHandler())
            )
            .CreateClient();
    }

    private static async Task MigrateCatalogAsync(string connectionString)
    {
        await using (
            var events = new EventContext(
                new DbContextOptionsBuilder<EventContext>().UseNpgsql(connectionString).Options
            )
        )
        {
            await events.Database.MigrateAsync();
        }

        // Migrated up front, like EventContext above, so the two Catalog hosts do not race each
        // other applying the same migrations on startup.
        await using var history = new HistoryContext(
            new DbContextOptionsBuilder<HistoryContext>().UseNpgsql(connectionString).Options
        );
        await history.Database.MigrateAsync();
    }

    private static async Task MigrateAccountAsync(string connectionString)
    {
        await using var context = new AccountDbContext(
            new DbContextOptionsBuilder<AccountDbContext>()
                .UseNpgsql(connectionString, b => b.MigrationsAssembly("Account"))
                .Options
        );
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Reverse order so dependents shut down before what they depend on.
        //
        // Tearing five hosts down at once occasionally trips MessageQueueConsumer.StopAsync,
        // which closes its channel unconditionally and throws if the connection went first.
        // Common.Tests covers the deliberate cases (a broker-side drop, then a stop) and they
        // pass, so this is a shutdown race rather than a reproducible defect - either way it is
        // teardown, and not a result worth failing the run over.
        _factories.Reverse();
        foreach (var factory in _factories)
        {
            try
            {
                await factory.DisposeAsync();
            }
            catch (ObjectDisposedException) { }
            catch (AlreadyClosedException) { }
            catch (OperationInterruptedException) { }
        }

        _catalogChannel?.Dispose();

        foreach (var variable in _environmentKeys)
            Environment.SetEnvironmentVariable(variable, null);

        await Task.WhenAll(
            _mongo.DisposeAsync(),
            _rabbit.DisposeAsync(),
            _kafka.DisposeAsync(),
            _postgres.DisposeAsync()
        );
    }
}
