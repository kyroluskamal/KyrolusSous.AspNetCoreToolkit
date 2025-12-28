namespace KyrolusSous.Repositories.EF.Generator.TestApp.Models;

public class AuditableSoftDeletableEntity : AuditableEntity
{
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}

