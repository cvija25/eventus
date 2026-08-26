using Contracts.Messaging;
using Game.Consumers;
using Game.GrpcClients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHostedService<GameCommandConsumer>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddGrpcClient<Catalog.GRPC.Catalog.CatalogClient>(o =>
{
    o.Address = new Uri(builder.Configuration["GrpcSettings:CatalogUrl"]!);
});
builder.Services.AddScoped<CatalogGrpcClient>();
var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet(
    "/",
    () =>
    {
        return "Hello world!";
    }
);
app.MapControllers();
app.Run();
