namespace KyrolusSous.Repositories.EF.Abstractions.Auditing;

/// <summary>
/// Defines creation and modification auditing timestamps and actor identifiers.
/// </summary>
public interface IKyrolusAuditableEntity
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was created.
    /// </summary>
    DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the actor who created the entity.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was last modified.
    /// </summary>
    DateTime? LastModifiedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the actor who last modified the entity.
    /// </summary>
    string? LastModifiedBy { get; set; }
}

/// <summary>
/// Extends <see cref="IKyrolusAuditableEntity"/> with soft-deletion audit metadata.
/// </summary>
public interface IKyrolusFullAuditableEntity : IKyrolusAuditableEntity
{
    /// <summary>
    /// Gets or sets whether the entity is marked as soft-deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was deleted.
    /// </summary>
    DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the actor who deleted the entity.
    /// </summary>
    string? DeletedBy { get; set; }
}
