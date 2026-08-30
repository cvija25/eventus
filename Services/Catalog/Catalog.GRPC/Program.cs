using Catalog.Common.Extensions;
using Catalog.GRPC;
using Catalog.GRPC.Mappings;
using Catalog.GRPC.Publishers;
using Catalog.GRPC.Services;
using Common.Messaging;
using Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddSingleton<IPriceUpdatePublisher, PriceUpdatePublisher>();
builder.Services.AddCatalogCommon(builder.Configuration);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<CatalogGrpcMappingProfile>());
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));

var app = builder.Build();
await app.MigrateCatalogDatabase();

app.MapGrpcService<CatalogService>();
app.MapGrpcReflectionService();

app.Run();

public partial class Program { }
