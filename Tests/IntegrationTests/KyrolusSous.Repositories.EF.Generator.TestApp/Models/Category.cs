using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Category),
    typeof(Guid),
    "Id",
    EnableSoftDelete = true,
    IncludeProperties = new[] { "ProductCategories", "Store" },
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class Category : AuditableSoftDeletableEntity
{
    public Guid StoreId { get; set; }
    public virtual Store? Store { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public virtual ICollection<ProductCategory> ProductCategories { get; set; } = [];

}
