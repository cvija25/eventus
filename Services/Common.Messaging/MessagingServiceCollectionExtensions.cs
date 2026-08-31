using Contracts.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqOptions(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        Require(configuration, "RabbitMq:HostName");
        Require(configuration, "RabbitMq:Port");
        Require(configuration, "RabbitMq:UserName");
        Require(configuration, "RabbitMq:Password");

        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        return services;
    }

    public static IServiceCollection AddKafkaOptions(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        Require(configuration, "Kafka:BootstrapServers");
        Require(configuration, "Kafka:PriceUpdateTopic");

        services.Configure<KafkaOptions>(configuration.GetSection("Kafka"));
        return services;
    }

    private static void Require(IConfiguration configuration, string key)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
            throw new InvalidOperationException(
                $"Configuration key '{key}' is missing. Containers read it from "
                    + $"compose.override.yaml as '{key.Replace(":", "__")}'; IDE runs read it "
                    + "from appsettings.Development.json."
            );
    }
}
