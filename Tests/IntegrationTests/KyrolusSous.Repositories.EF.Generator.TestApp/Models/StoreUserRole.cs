using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(StoreUserRole),
    typeof(Guid),
    "StoreId",
    "UserId",
    "RoleId",
    EnableCaching = true,
    CacheTtlSeconds = 300,
    AsNoTracking = true,
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class StoreUserRole
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid StoreId { get; set; }
    public virtual Store? Store { get; set; }
    public Guid UserId { get; set; }
    public virtual User? User { get; set; }
    public Guid RoleId { get; set; }
    public virtual Role? Role { get; set; }
}
