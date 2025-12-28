using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Review),
    typeof(Guid),
    "ProductId",
    "CustomerId",
    EnableCaching = true,
    CacheTtlSeconds = 300,
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class Review
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public Guid CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
