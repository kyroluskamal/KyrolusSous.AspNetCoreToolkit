using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

[KyrolusEfRepository(
    typeof(ApplicationDbContext),
    typeof(Tenant),
    typeof(Guid),
    "Id",
    IncludeProperties = new[] { "Stores" },
    Namespace = "KyrolusSous.Repositories.EF.Generator.TestApp.Repositories")]
public class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Domain { get; set; }
    public virtual ICollection<Store> Stores { get; set; } = [];
}
