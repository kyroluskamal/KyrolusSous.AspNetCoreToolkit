namespace KyrolusSous.Audit.Abstractions;

public enum KyrolusAuditAction
{
    Create,
    Update,
    Delete
}

public sealed record KyrolusPropertyChange
{
    public required string PropertyName { get; init; }
    public object? OriginalValue { get; init; }
    public object? NewValue { get; init; }
}

public sealed record KyrolusAuditEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string EntityName { get; init; }
    public required string EntityId { get; init; }
    public required KyrolusAuditAction Action { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? TenantId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<KyrolusPropertyChange> Changes { get; init; } = [];
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusAuditableAttribute : Attribute
{
    public bool Enabled { get; init; } = true;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusAuditIgnoreAttribute : Attribute;

public interface IKyrolusAuditContextProvider
{
    string? GetCurrentUserId();
    string? GetCurrentUserName();
    string? GetCurrentTenantId();
    string? GetCurrentIpAddress();
    string? GetCurrentUserAgent();
}

public interface IKyrolusAuditStore
{
    Task SaveAuditEntriesAsync(IEnumerable<KyrolusAuditEntry> entries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusAuditEntry>> GetEntityHistoryAsync(string entityName, string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusAuditEntry>> GetUserActivityAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
}
