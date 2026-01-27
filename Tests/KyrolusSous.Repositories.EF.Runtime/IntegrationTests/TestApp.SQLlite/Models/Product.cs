using KyrolusSous.Repositories.EF.Abstractions;
namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

public class Product : AuditableSoftDeletableEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
    public required string Name { get; set; }
    public required string Sku { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
    public virtual ICollection<ProductCategory> ProductCategories { get; set; } = [];
    public virtual ICollection<Review> Reviews { get; set; } = [];
    public virtual ICollection<OrderLine> OrderLines { get; set; } = [];
}


