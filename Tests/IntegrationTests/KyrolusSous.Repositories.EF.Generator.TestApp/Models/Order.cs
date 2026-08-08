using KyrolusSous.Repositories.EF.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Order),
    typeof(Guid),
    "Id",
    RowVersionProperty = "RowVersion",
    IncludeProperties = new[] { "OrderLines", "Payment", "Customer" },
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class Order : AuditableEntity
{
    public Guid StoreId { get; set; }
    public virtual Store? Store { get; set; }
    public Guid CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }
    public required string OrderNumber { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal Total { get; set; }
    public byte[]? RowVersion { get; set; }
    public virtual ICollection<OrderLine> OrderLines { get; set; } = [];
    public virtual Payment? Payment { get; set; }
}

public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Completed,
    Cancelled
}
