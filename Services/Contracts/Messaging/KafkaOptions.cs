namespace Contracts.Messaging;

public class KafkaOptions
{
    public required string BootstrapServers { get; set; }
    public required string PriceUpdateTopic { get; set; }
}