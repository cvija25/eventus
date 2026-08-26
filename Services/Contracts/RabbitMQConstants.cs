namespace Contracts;

public class RabbitMQConstants
{
    // renamed queues: use command-request for incoming commands and command-response for results
    public const string GameCommandQueue = "command-request";
    public const string CommandApprovedQueue = "command-response";
    public const string EventResolvedQueue = "event-resolved";
}
