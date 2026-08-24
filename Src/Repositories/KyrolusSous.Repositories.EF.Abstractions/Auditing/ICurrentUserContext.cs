namespace KyrolusSous.Repositories.EF.Abstractions.Auditing;

/// <summary>
/// Provides ambient context regarding the currently authenticated user / actor.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Gets the unique identifier of the currently authenticated user (or <c>null</c> if unauthenticated/system).
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the username or email of the currently authenticated user.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Gets whether the current context is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}

/// <summary>
/// Represents a structured property-level change record captured during database commits.
/// </summary>
public sealed class KyrolusAuditEntry
{
    /// <summary>
    /// Gets or sets the database table / entity name.
    /// </summary>
    public string EntityName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation type (<c>"Insert"</c>, <c>"Update"</c>, or <c>"Delete"</c>).
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the actor who performed the change.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the commit.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the primary key values of the affected entity.
    /// </summary>
    public Dictionary<string, object?> KeyValues { get; init; } = new();

    /// <summary>
    /// Gets or sets the previous property values before modification.
    /// </summary>
    public Dictionary<string, object?> OldValues { get; init; } = new();

    /// <summary>
    /// Gets or sets the new property values after modification.
    /// </summary>
    public Dictionary<string, object?> NewValues { get; init; } = new();

    /// <summary>
    /// Gets or sets the list of modified property names.
    /// </summary>
    public List<string> ChangedColumns { get; init; } = [];
}
