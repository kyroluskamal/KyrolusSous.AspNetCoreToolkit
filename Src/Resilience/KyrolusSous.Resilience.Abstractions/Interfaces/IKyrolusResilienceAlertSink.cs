namespace KyrolusSous.Resilience;

/// <summary>
/// Alert notification payload published when a circuit breaker state transition or resilience anomaly occurs.
/// </summary>
public sealed record KyrolusResilienceAlert(
    string PipelineName,
    KyrolusCircuitState NewState,
    string Message,
    DateTimeOffset TimestampUtc);

/// <summary>
/// Sink for publishing real-time alerts to monitoring channels (Webhooks, Slack, Teams, PagerDuty).
/// </summary>
public interface IKyrolusResilienceAlertSink
{
    /// <summary>
    /// Publishes an alert asynchronously.
    /// </summary>
    Task PublishAlertAsync(KyrolusResilienceAlert alert, CancellationToken cancellationToken = default);
}
