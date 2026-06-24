namespace Contracts;

public enum EventOutcome
{
    Yes = 0,
    No = 1,
}

public class EventResolvedEvent
{
    public Guid EventId { get; set; }
    public EventOutcome Outcome { get; set; }
}
