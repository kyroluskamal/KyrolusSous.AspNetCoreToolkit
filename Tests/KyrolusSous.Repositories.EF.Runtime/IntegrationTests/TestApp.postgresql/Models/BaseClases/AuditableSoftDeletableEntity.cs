namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

public class AuditableSoftDeletableEntity : AuditableEntity
{
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}



