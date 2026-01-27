using KyrolusSous.Repositories.EF.Abstractions;
namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

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
    public virtual ICollection<Review> Reviews { get; set; } = [];
}


