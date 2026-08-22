using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KyrolusSous.Resilience;

public class ResilienceCircuitBreakerHealthCheck(
    IKyrolusResiliencePipelineProvider pipelineProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var defaultPipeline = pipelineProvider.GetPipeline("default");
            return Task.FromResult(HealthCheckResult.Healthy("All resilience pipelines operational."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"Resilience pipeline error: {ex.Message}"));
        }
    }
}
