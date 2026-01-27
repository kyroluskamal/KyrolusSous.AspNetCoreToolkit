using KyrolusSous.Repositories.EF.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

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


