using System.ComponentModel.DataAnnotations;
using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(OrderLine),
    typeof(Guid),
    "OrderId",
    "ProductId",
    IncludeProperties = new[] { "Product" },
    EnableCaching = true,
    CacheTtlSeconds = 300,
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class OrderLine
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    [Key]
    public Guid OrderId { get; set; }
    public virtual Order? Order { get; set; }
    [Key]
    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
