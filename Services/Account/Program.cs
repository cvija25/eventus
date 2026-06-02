using Account.Consumers;
using Account.Messaging;
using Account.Publishers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHostedService<BetApprovedConsumer>();
builder.Services.AddSingleton<BetPlacedPublisher>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet(
    "/",
    () => { return "Hello world!"; }
);
app.MapControllers();
app.Run();