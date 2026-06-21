namespace Contracts;

public class BetApprovedEvent
{
    public Guid AccId;
    public decimal Stake;
    public bool IsApproved { get; set; }
    public DateTime ApprovedAt { get; set; }
}