using Account.Consumers;
using Account.Messaging;
using Account.Publishers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddHostedService<BetApprovedConsumer>();
builder.Services.AddSingleton<BetPlacedPublisher>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet(
    "/",
    () => { return "Hello world!"; }
);
app.UseCors("Frontend");
app.MapControllers();
app.Run();