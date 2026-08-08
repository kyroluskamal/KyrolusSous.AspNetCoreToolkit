using System.ComponentModel.DataAnnotations;
using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

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


