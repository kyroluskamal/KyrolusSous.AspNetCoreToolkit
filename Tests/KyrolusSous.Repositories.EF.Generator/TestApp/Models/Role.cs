using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Role),
    typeof(Guid),
    "Id",
    EnableSoftDelete = false,
    IncludeProperties = new[] { "StoreUserRoles" },
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public virtual ICollection<StoreUserRole> StoreUserRoles { get; set; } = [];
}
