namespace KyrolusSous.CQRS.Abstractions.Audit;

/// <summary>
/// In-memory audit sink designed for testing and lightweight local environments.
/// </summary>
public sealed class KyrolusInMemoryAuditSink : IKyrolusAuditSink
{
    private readonly ConcurrentBag<KyrolusAuditEntry> _entries = [];

    public IReadOnlyCollection<KyrolusAuditEntry> Entries => [.. _entries];

    public Task EmitAsync(KyrolusAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public void Clear() => _entries.Clear();
}
