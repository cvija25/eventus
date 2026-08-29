namespace Contracts;

public class RabbitMQConstants
{
    public const string GameCommandQueue = "command-request";
    public const string CommandApprovedQueue = "command-response";
    public const string EventResolvedQueue = "event-resolved";
    public const string PriceUpdateQueue = "price-update";
}
