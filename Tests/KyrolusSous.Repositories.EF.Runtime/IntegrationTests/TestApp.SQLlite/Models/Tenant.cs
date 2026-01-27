using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Domain { get; set; }
    public virtual ICollection<Store> Stores { get; set; } = [];
}


