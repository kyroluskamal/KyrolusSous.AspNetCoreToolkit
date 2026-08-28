namespace KyrolusSous.Resilience;

/// <summary>
/// Handler invoked when a resilience alert is published. Implement to forward alerts to Slack, Teams, PagerDuty, or Webhooks.
/// </summary>
public interface IKyrolusResilienceAlertHandler
{
    /// <summary>
    /// Handles an alert published by the resilience alert sink.
    /// </summary>
    ValueTask HandleAlertAsync(KyrolusResilienceAlert alert, CancellationToken cancellationToken = default);
}
