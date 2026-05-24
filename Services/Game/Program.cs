var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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