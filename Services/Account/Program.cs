using Account.Consumers;
using Account.Data;
using Account.Mappings;
using Account.Publishers;
using Account.Repositories;
using Common.Messaging;
using Common.Web;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCurrentUser();
builder.Services.AddHostedService<CommandApprovedConsumer>();
builder.Services.AddHostedService<EventResolvedEventConsumer>();
builder.Services.AddSingleton<IGameCommandPublisher, GameCommandPublisher>();
builder.Services.AddRabbitMqOptions(builder.Configuration);

builder.Services.AddDbContext<AccountDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("AccountDb"),
        b => b.MigrationsAssembly("Account")
    )
);
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ICommandApprovedHandler, CommandApprovedHandler>();
builder.Services.AddScoped<IEventResolvedHandler, EventResolvedHandler>();
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<TransactionMappingProfile>());

builder.Services.AddEventusJwtAuth(builder.Configuration);

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
