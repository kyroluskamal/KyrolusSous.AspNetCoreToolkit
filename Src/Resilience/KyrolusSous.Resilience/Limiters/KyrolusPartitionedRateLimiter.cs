using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Resilience;

/// <summary>
/// Partitioned concurrency rate limiter isolating limits per tenant, client IP, or user key.
/// Hardened against memory exhaustion and unbounded partition creation.
/// </summary>
public class KyrolusPartitionedRateLimiter : IKyrolusPartitionedRateLimiter
{
    private const int MaxTrackedPartitions = 50000;

    private sealed class PartitionState
    {
        public int InFlight;
        public long LastAccessTicks;
    }

    private readonly ConcurrentDictionary<string, PartitionState> _partitions = new(StringComparer.Ordinal);
    private readonly IOptionsMonitor<KyrolusResilienceOptions>? _optionsMonitor;
    private readonly KyrolusResilienceOptions _staticOptions;

    public KyrolusPartitionedRateLimiter(
        IOptionsMonitor<KyrolusResilienceOptions>? optionsMonitor = null,
        IOptions<KyrolusResilienceOptions>? options = null)
    {
        _optionsMonitor = optionsMonitor;
        _staticOptions = options?.Value ?? new KyrolusResilienceOptions();
    }

    private int MaxPermits => (_optionsMonitor?.CurrentValue ?? _staticOptions).PartitionedRateLimiter.PermitsPerPartition;

    public bool TryAcquire(string partitionKey)
    {
        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            partitionKey = "default_partition";
        }

        // Memory protection against unbound partition growth
        if (_partitions.Count >= MaxTrackedPartitions && !_partitions.ContainsKey(partitionKey))
        {
            PruneInactivePartitions();
        }

        var state = _partitions.GetOrAdd(partitionKey, _ => new PartitionState { LastAccessTicks = Environment.TickCount64 });
        Volatile.Write(ref state.LastAccessTicks, Environment.TickCount64);

        var limit = MaxPermits > 0 ? MaxPermits : 20;

        while (true)
        {
            var current = Volatile.Read(ref state.InFlight);
            if (current >= limit)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref state.InFlight, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    public void Release(string partitionKey)
    {
        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            partitionKey = "default_partition";
        }

        if (_partitions.TryGetValue(partitionKey, out var state))
        {
            Volatile.Write(ref state.LastAccessTicks, Environment.TickCount64);

            while (true)
            {
                var current = Volatile.Read(ref state.InFlight);
                if (current <= 0)
                {
                    break;
                }

                if (Interlocked.CompareExchange(ref state.InFlight, current - 1, current) == current)
                {
                    break;
                }
            }
        }
    }

    private void PruneInactivePartitions()
    {
        var cutoff = Environment.TickCount64 - 60000; // 1 minute inactivity
        foreach (var kvp in _partitions)
        {
            if (Volatile.Read(ref kvp.Value.InFlight) == 0 && Volatile.Read(ref kvp.Value.LastAccessTicks) < cutoff)
            {
                _partitions.TryRemove(kvp.Key, out _);
            }
        }
    }
}
