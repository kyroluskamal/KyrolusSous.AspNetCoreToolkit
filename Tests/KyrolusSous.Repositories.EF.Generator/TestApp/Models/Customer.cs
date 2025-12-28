using KyrolusSous.Repositories.EF.Abstractions;
using System.Text.Json.Serialization;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Customer),
    typeof(Guid),
    "Id",
    EnableSoftDelete = true,
    SoftDeleteProperty = "IsDeleted",
    IncludeProperties = new[] { "Store", "Orders", "Reviews" },
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class Customer : AuditableSoftDeletableEntity, IHasTenant
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public Address Address { get; set; } = new Address();
    public Guid StoreId { get; set; }
    public virtual Store? Store { get; set; }
    public Guid TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = [];
    [JsonIgnore]
    public virtual ICollection<Review> Reviews { get; set; } = [];
}
