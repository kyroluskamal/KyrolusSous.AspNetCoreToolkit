using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KyrolusSous.RabbitMQ.Runtime.Health;

/// <summary>
/// Health check that verifies active connectivity and channel creation with RabbitMQ broker with timeout guards.
/// </summary>
public sealed class KyrolusRabbitMQHealthCheck(IKyrolusRabbitMQConnection connection) : IHealthCheck
{
    private readonly IKyrolusRabbitMQConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(DefaultProbeTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Verify channel creation succeeds within probe timeout
            using var channel = await _connection.CreateChannelAsync(linkedCts.Token).ConfigureAwait(false);
            if (!channel.IsOpen)
            {
                return HealthCheckResult.Degraded("RabbitMQ channel could not be opened.");
            }

            return HealthCheckResult.Healthy("RabbitMQ broker connection is healthy and operational.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"RabbitMQ health check timed out after {DefaultProbeTimeout.TotalSeconds} seconds.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ health check failed.", ex);
        }
    }
}
