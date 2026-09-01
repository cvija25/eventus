using Catalog.API.Consumers;
using Catalog.API.Publishers;
using Catalog.API.Services;
using Catalog.Common.Extensions;
using Common.Messaging;
using Common.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen();

builder.Services.AddControllers();
builder.Services.AddCurrentUser();
builder.Services.AddCatalogCommon(builder.Configuration);
builder.Services.AddHostedService<PriceUpdateConsumer>();
builder.Services.AddKafkaOptions(builder.Configuration);
builder.Services.AddSingleton<ISseBroadcaster, SseBroadcaster>();
builder.Services.AddSingleton<IEventResolvedPublisher, EventResolvedPublisher>();
builder.Services.AddScoped<IPriceUpdateHandler, PriceUpdateHandler>();
builder.Services.AddRabbitMqOptions(builder.Configuration);

builder.Services.AddEventusJwtAuth(builder.Configuration);

var app = builder.Build();
await app.MigrateCatalogDatabase();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
