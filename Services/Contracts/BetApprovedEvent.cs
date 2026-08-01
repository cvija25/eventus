namespace Contracts;

public class BetApprovedEvent
{
    public Guid AccId { get; set; }
    public decimal Stake { get; set; }
    public bool IsApproved { get; set; }
    public DateTime ApprovedAt { get; set; }
}
