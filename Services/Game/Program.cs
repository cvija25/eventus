using Game.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHostedService<BetPlacedConsumer>();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet(
    "/",
    () =>
    {
        return "Hello world!";
    }
);
app.MapControllers();
app.Run();