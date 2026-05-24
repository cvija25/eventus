var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/game", () =>
{
    return "hello game";
});
app.Run();