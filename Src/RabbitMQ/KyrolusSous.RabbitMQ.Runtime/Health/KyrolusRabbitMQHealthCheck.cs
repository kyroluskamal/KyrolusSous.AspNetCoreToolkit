using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KyrolusSous.RabbitMQ.Runtime.Health;

/// <summary>
/// Health check that verifies active connectivity and channel creation with RabbitMQ broker.
/// </summary>
public sealed class KyrolusRabbitMQHealthCheck(IKyrolusRabbitMQConnection connection) : IHealthCheck
{
    private readonly IKyrolusRabbitMQConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_connection.Connection is null || !_connection.Connection.IsOpen)
            {
                return HealthCheckResult.Unhealthy("RabbitMQ connection is closed or uninitialized.");
            }

            // Verify channel creation succeeds
            using var channel = await _connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
            if (!channel.IsOpen)
            {
                return HealthCheckResult.Degraded("RabbitMQ channel could not be opened.");
            }

            return HealthCheckResult.Healthy("RabbitMQ broker connection is healthy and operational.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ health check failed.", ex);
        }
    }
}
