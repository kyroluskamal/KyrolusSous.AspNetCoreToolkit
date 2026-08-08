using KyrolusSous.Repositories.EF.Abstractions;
namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Product),
    typeof(Guid),
    "Id",
    EnableSoftDelete = true,
    SoftDeleteProperty = "IsDeleted",
    RowVersionProperty = "RowVersion",
    IncludeProperties = new[] { "ProductCategories", "OrderLines" },
    AsNoTracking = false,
    UseSplitQuery = true,
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
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
