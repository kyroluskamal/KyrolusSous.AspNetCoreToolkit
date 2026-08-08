using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

public class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public virtual ICollection<StoreUserRole> StoreUserRoles { get; set; } = [];
}


