namespace Catalog.Common.Entities;

public class PriceHistory
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public required decimal PriceYes { get; set; }
    public required decimal PriceNo { get; set; }
    public required DateTime Timestamp { get; set; }
}
