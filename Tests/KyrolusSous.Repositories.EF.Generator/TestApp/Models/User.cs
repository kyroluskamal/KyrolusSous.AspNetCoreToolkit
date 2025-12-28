using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(User),
    typeof(Guid),
    "Id",
    IncludeProperties = new[] { "StoreUserRoles" },
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class User
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public virtual ICollection<StoreUserRole> StoreUserRoles { get; set; } = [];
}
