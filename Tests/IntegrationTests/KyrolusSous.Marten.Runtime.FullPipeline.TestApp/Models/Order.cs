using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;

public sealed class Order
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = [];
    public Guid? PaymentId { get; set; }
    public List<string>? Tags { get; set; }
    public List<Guid>? PaymentIds { get; set; }
    public Guid[]? PaymentArrayIds { get; set; }
    public List<Guid>? PaymentSetIds { get; set; }
    public Payment? Payment { get; set; }
    public List<Payment>? Payments { get; set; }
    public Payment[]? PaymentArray { get; set; }
    public HashSet<Payment>? PaymentSet { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = UtcTimestamp.DateTimeOffsetNow();
    public DateOnly BusinessDate { get; set; }
    public TimeOnly BusinessTime { get; set; }
    public TimeSpan FulfillmentWindow { get; set; }
}

public sealed class OrderLine
{
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public TimeOnly? ScheduledTime { get; set; }
    public TimeSpan? PrepDuration { get; set; }
}

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2
}
