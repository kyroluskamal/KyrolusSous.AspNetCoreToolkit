using System.Collections.Concurrent;

namespace KyrolusSous.Resilience;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IKyrolusFallbackRegistry"/>.
/// </summary>
public class KyrolusFallbackRegistry : IKyrolusFallbackRegistry
{
    private readonly ConcurrentDictionary<(string PipelineName, Type ResultType), object> _fallbacks = new();

    public KyrolusFallbackRegistry(IEnumerable<IKyrolusFallbackRegistration>? registrations = null)
    {
        if (registrations is not null)
        {
            foreach (var reg in registrations)
            {
                reg.Register(this);
            }
        }
    }

    public void RegisterFallback<TResult>(string pipelineName, Func<Exception, CancellationToken, ValueTask<TResult>> fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(fallback);

        _fallbacks[(pipelineName, typeof(TResult))] = fallback;
    }

    public bool TryGetFallback<TResult>(string pipelineName, out Func<Exception, CancellationToken, ValueTask<TResult>>? fallback)
    {
        if (_fallbacks.TryGetValue((pipelineName, typeof(TResult)), out var obj) &&
            obj is Func<Exception, CancellationToken, ValueTask<TResult>> typedFallback)
        {
            fallback = typedFallback;
            return true;
        }

        fallback = null;
        return false;
    }
}
