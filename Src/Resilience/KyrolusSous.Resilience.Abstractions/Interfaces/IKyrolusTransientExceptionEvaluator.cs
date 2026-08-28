namespace KyrolusSous.Resilience;

/// <summary>
/// Contract for evaluating whether an exception is considered transient (retryable).
/// Can be registered by domain-specific packages (e.g. EF Core, Redis, Elasticsearch, RabbitMQ) to enrich resilience decision-making.
/// </summary>
public interface IKyrolusTransientExceptionEvaluator
{
    /// <summary>
    /// Evaluates if the given exception is a transient, recoverable failure.
    /// </summary>
    bool IsTransient(Exception exception);
}
