using System.ComponentModel.DataAnnotations;

namespace Account.Entities;

public class Wallet
{
    [Key]
    public Guid AccountId { get; set; }

    public decimal AvailableFunds { get; set; }
}
