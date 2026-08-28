namespace KyrolusSous.Resilience;

/// <summary>
/// Registration descriptor for a typed fallback handler.
/// </summary>
public interface IKyrolusFallbackRegistration
{
    void Register(IKyrolusFallbackRegistry registry);
}

/// <summary>
/// Concrete registration descriptor for a typed fallback handler.
/// </summary>
public sealed class KyrolusFallbackRegistration<TResult>(
    string pipelineName,
    Func<Exception, CancellationToken, ValueTask<TResult>> fallback) : IKyrolusFallbackRegistration
{
    public void Register(IKyrolusFallbackRegistry registry)
    {
        registry.RegisterFallback(pipelineName, fallback);
    }
}
