using System.Collections.Concurrent;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior providing concurrency limiting and throttling on requests implementing <see cref="IThrottledRequest"/>.
/// </summary>
[PipelineOrder(-750)]
public sealed class KyrolusThrottlingBehavior<TRequest, TResponse>(
    ILogger<KyrolusThrottlingBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Semaphores = new(StringComparer.Ordinal);
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IThrottledRequest throttled)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var throttleKey = string.IsNullOrWhiteSpace(throttled.ThrottleKey)
            ? typeof(TRequest).FullName ?? typeof(TRequest).Name
            : throttled.ThrottleKey.Trim();
        var maxConcurrency = throttled.MaxConcurrentExecutions <= 0 ? 5 : throttled.MaxConcurrentExecutions;
        var timeout = throttled.ThrottleTimeout;

        var semaphore = Semaphores.GetOrAdd(throttleKey, _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));

        var acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            _logger?.LogWarning(
                "[Kyrolus CQRS] Throttling rejected request {RequestType} for key '{ThrottleKey}'. Exceeded limit of {MaxConcurrency} within timeout {TimeoutMs}ms",
                typeof(TRequest).Name,
                throttleKey,
                maxConcurrency,
                timeout.TotalMilliseconds);

            throw new TimeoutException($"[Kyrolus CQRS] Request '{typeof(TRequest).Name}' was throttled. Concurrency limit of {maxConcurrency} reached for key '{throttleKey}'.");
        }

        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Clears cached semaphores (useful for test resets and memory reclamation).
    /// </summary>
    public static void ClearSemaphores() => Semaphores.Clear();
}
