using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Resilience;

/// <summary>
/// Default implementation of <see cref="IKyrolusChaosEngine"/> injecting controlled latency and faults.
/// </summary>
public class KyrolusChaosEngine : IKyrolusChaosEngine
{
    private readonly IOptionsMonitor<KyrolusResilienceOptions>? _optionsMonitor;
    private readonly KyrolusResilienceOptions _staticOptions;
    private readonly ILogger<KyrolusChaosEngine>? _logger;

    public KyrolusChaosEngine(
        IOptionsMonitor<KyrolusResilienceOptions>? optionsMonitor = null,
        IOptions<KyrolusResilienceOptions>? options = null,
        ILogger<KyrolusChaosEngine>? logger = null)
    {
        _optionsMonitor = optionsMonitor;
        _staticOptions = options?.Value ?? new KyrolusResilienceOptions();
        _logger = logger;
    }

    private KyrolusResilienceOptions CurrentOptions => _optionsMonitor?.CurrentValue ?? _staticOptions;

    public async ValueTask MaybeInjectFaultAsync(string pipelineName, CancellationToken cancellationToken = default)
    {
        var chaos = CurrentOptions.Chaos;
        if (!chaos.Enabled || chaos.InjectionRate <= 0)
        {
            return;
        }

        var roll = RandomNumberGenerator.GetInt32(0, 10000) / 10000.0;
        if (roll > chaos.InjectionRate)
        {
            return;
        }

        _logger?.LogWarning("Chaos Engine injecting fault on pipeline '{Pipeline}' (Rate: {Rate}).", pipelineName, chaos.InjectionRate);

        if (chaos.InjectedLatencyMs > 0)
        {
            await Task.Delay(chaos.InjectedLatencyMs, cancellationToken);
        }

        if (chaos.InjectTransientErrors)
        {
            throw new HttpRequestException($"Simulated Chaos fault on pipeline '{pipelineName}'.", null, System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }
}
