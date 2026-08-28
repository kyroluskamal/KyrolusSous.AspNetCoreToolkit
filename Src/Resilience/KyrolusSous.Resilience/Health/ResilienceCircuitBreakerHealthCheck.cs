using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KyrolusSous.Resilience;

/// <summary>
/// Health check probing the state of circuit breakers across all registered resilience pipelines.
/// </summary>
public class KyrolusResilienceCircuitBreakerHealthCheck(
    IKyrolusResiliencePipelineProvider pipelineProvider,
    IKyrolusCircuitBreakerObserver? circuitObserver = null) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (circuitObserver is not null)
            {
                var states = circuitObserver.GetAllCircuitStates();
                var openCircuits = states.Where(kvp => kvp.Value == KyrolusCircuitState.Open).Select(kvp => kvp.Key).ToList();

                if (openCircuits.Count > 0)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Circuit breaker open for pipelines: {string.Join(", ", openCircuits)}",
                        data: states.ToDictionary(k => k.Key, v => (object)v.Value.ToString())));
                }
            }

            var defaultPipeline = pipelineProvider.GetPipeline("default");
            return Task.FromResult(HealthCheckResult.Healthy("All resilience pipelines operational."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"Resilience pipeline error: {ex.Message}"));
        }
    }
}
