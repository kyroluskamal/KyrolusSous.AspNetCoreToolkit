using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

public class Payment
{
    public Guid OrderId { get; set; }
    public virtual Order? Order { get; set; }
    public required string Provider { get; set; }
    public required string ProviderRef { get; set; }
    public required decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTimeOffset? PaidAt { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed
}


