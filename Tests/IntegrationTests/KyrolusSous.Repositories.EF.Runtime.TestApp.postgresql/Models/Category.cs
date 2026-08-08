using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

public class Category : AuditableSoftDeletableEntity
{
    public Guid StoreId { get; set; }
    public virtual Store? Store { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public virtual ICollection<ProductCategory> ProductCategories { get; set; } = [];

}


