using System.ComponentModel.DataAnnotations;
using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(ProductCategory),
    typeof(Guid),
    "ProductId",
    "CategoryId",
    IncludeProperties = new[] { "Product", "Category" },
    EnableCaching = true,
    CacheTtlSeconds = 300,
    AsNoTracking = true,
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class ProductCategory
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    [Key]
    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }
    [Key]
    public Guid CategoryId { get; set; }
    public virtual Category? Category { get; set; }
}
