using System.Collections.Concurrent;

namespace KyrolusSous.CQRS.Abstractions.Audit;

/// <summary>
/// In-memory audit sink designed for testing and lightweight local environments.
/// </summary>
public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly ConcurrentBag<KyrolusAuditEntry> _entries = [];

    public IReadOnlyCollection<KyrolusAuditEntry> Entries => _entries.ToArray();

    public Task EmitAsync(KyrolusAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public void Clear() => _entries.Clear();
}
