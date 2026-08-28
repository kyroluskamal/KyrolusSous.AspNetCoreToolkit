using Microsoft.Extensions.Logging;

namespace KyrolusSous.Resilience;

/// <summary>
/// Default alert sink broadcasting resilience alerts to all registered <see cref="IKyrolusResilienceAlertHandler"/> subscribers and logger.
/// </summary>
public class KyrolusCompositeResilienceAlertSink : IKyrolusResilienceAlertSink
{
    private readonly IEnumerable<IKyrolusResilienceAlertHandler> _handlers;
    private readonly ILogger<KyrolusCompositeResilienceAlertSink>? _logger;

    public KyrolusCompositeResilienceAlertSink(
        IEnumerable<IKyrolusResilienceAlertHandler>? handlers = null,
        ILogger<KyrolusCompositeResilienceAlertSink>? logger = null)
    {
        _handlers = handlers?.ToList() ?? [];
        _logger = logger;
    }

    public async Task PublishAlertAsync(KyrolusResilienceAlert alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        _logger?.LogWarning("Resilience Alert: Pipeline '{Pipeline}' transitioned to '{State}'. Message: {Message}",
            alert.PipelineName, alert.NewState, alert.Message);

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.HandleAlertAsync(alert, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling resilience alert in handler {HandlerType}.", handler.GetType().Name);
            }
        }
    }
}
