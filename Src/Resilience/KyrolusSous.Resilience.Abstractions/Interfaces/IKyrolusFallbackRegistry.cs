namespace KyrolusSous.Resilience;

/// <summary>
/// Registry for declarative, type-safe fallback handlers executed when a resilience pipeline fails.
/// </summary>
public interface IKyrolusFallbackRegistry
{
    /// <summary>
    /// Registers a fallback delegate for a specific pipeline name and return type.
    /// </summary>
    void RegisterFallback<TResult>(string pipelineName, Func<Exception, CancellationToken, ValueTask<TResult>> fallback);

    /// <summary>
    /// Tries to resolve a registered fallback delegate for a given pipeline name and return type.
    /// </summary>
    bool TryGetFallback<TResult>(string pipelineName, out Func<Exception, CancellationToken, ValueTask<TResult>>? fallback);
}
