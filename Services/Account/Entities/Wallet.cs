using System.ComponentModel.DataAnnotations;

namespace Account.Entities;

public class Wallet
{
    [Key]
    public Guid AvailableFunds { get; set; }

    public decimal Amount { get; set; }
}
