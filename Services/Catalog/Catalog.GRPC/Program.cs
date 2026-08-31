using Catalog.Common.Extensions;
using Catalog.GRPC;
using Catalog.GRPC.Mappings;
using Catalog.GRPC.Publishers;
using Catalog.GRPC.Services;
using Common.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddSingleton<IPriceUpdatePublisher, PriceUpdatePublisher>();
builder.Services.AddCatalogCommon(builder.Configuration);
builder.Services.AddRabbitMqOptions(builder.Configuration);
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<CatalogGrpcMappingProfile>());
builder.Services.AddKafkaOptions(builder.Configuration);

var app = builder.Build();
await app.MigrateCatalogDatabase();

app.MapGrpcService<CatalogService>();
app.MapGrpcReflectionService();

app.Run();

public partial class Program { }
