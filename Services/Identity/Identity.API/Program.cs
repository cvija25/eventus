using Common.Web;
using Identity.API.Clients;
using Identity.API.Extensions;
using Identity.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    );
});

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddSingleton(builder.Configuration.GetJwtOptions());
builder.Services.AddSingleton<JwtService>();

var accountEndpoint =
    builder.Configuration["ApiEndpoints:Account"]
    ?? throw new InvalidOperationException(
        "Configuration key 'ApiEndpoints:Account' is missing. Containers read it from "
            + "compose.override.yaml as 'ApiEndpoints__Account'; IDE runs read it from "
            + "appsettings.Development.json."
    );
builder.Services.AddHttpClient<AccountClient>(client =>
    client.BaseAddress = new Uri(accountEndpoint)
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.MapControllers();
app.Run();
