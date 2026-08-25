namespace KyrolusSous.CQRS.Abstractions.Audit;

/// <summary>
/// Defines a target consumer or store for emitting audit entries.
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// Emits a single audit entry to the underlying storage or logging system.
    /// </summary>
    Task EmitAsync(KyrolusAuditEntry entry, CancellationToken cancellationToken = default);
}
