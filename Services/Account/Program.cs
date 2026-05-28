using Account.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHostedService<BetApprovedConsumer>();
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
