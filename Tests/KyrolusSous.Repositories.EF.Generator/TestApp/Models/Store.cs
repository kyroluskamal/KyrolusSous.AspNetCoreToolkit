using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Store),
    typeof(Guid),
    "Id",
    EnableSoftDelete = true,
    EnableCaching = true,
    CacheTtlSeconds = 10,
    SoftDeleteProperty = "IsDeleted",
    IncludeProperties = new[] { "StoreUserRoles", "Categories", "Customers" },
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class Store : AuditableSoftDeletableEntity
{
    public Guid TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    public required string Name { get; set; }
    public required string Locale { get; set; }

    public virtual ICollection<Product> Products { get; set; } = [];
    public virtual ICollection<Order> Orders { get; set; } = [];
    public virtual ICollection<StoreUserRole> StoreUserRoles { get; set; } = [];
    public virtual ICollection<Category> Categories { get; set; } = [];
    public virtual ICollection<Customer> Customers { get; set; } = [];
}
