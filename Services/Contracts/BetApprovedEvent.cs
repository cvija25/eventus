namespace Contracts;

public class BetApprovedEvent
{
    public Guid BetId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ApprovedAt { get; set; }
}