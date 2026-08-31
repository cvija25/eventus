using Common.Web;
using Identity.API.Entities;
using Identity.API.Mappings;
using Identity.API.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Identity.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var client = new MongoClient(configuration.Require("ConnectionStrings:IdentityDb"));
        var database = client.GetDatabase("eventus_identity");
        var users = database.GetCollection<User>("users");

        var indexModel = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true }
        );
        users.Indexes.CreateOne(indexModel);

        services.AddSingleton(client);
        services.AddSingleton(database);
        services.AddSingleton(users);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddAutoMapper(cfg => cfg.AddProfile<UserMappingProfile>());

        return services;
    }
}
