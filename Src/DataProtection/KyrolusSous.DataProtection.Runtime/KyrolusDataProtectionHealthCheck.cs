using KyrolusSous.DataProtection.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionHealthCheck(
    IKyrolusDataProtectionKeyRepository repository)
    : IHealthCheck
{
    private readonly IKyrolusDataProtectionKeyRepository repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await repository.ExportAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"DataProtection keys available: {keys.Count}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("DataProtection key repository is unavailable.", ex);
        }
    }
}
