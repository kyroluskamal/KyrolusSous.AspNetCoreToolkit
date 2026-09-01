namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// The default <see cref="IKyrolusValidationMetrics"/>: discards every recorded run. Registered by
/// <see cref="ServiceCollectionExtensions.AddKyrolusValidationRuntime"/> via <c>TryAddSingleton</c>, so
/// registering a real implementation before that call replaces it automatically.
/// </summary>
public sealed class KyrolusNoopValidationMetrics : IKyrolusValidationMetrics
{
    /// <summary>A shared, reusable instance, since this implementation has no state.</summary>
    public static readonly IKyrolusValidationMetrics Instance = new KyrolusNoopValidationMetrics();

    /// <inheritdoc />
    public ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default)
    => ValueTask.CompletedTask;
}

/// <summary>
/// <see cref="IKyrolusValidationMetrics"/> that forwards each recorded run to a supplied delegate - a shortcut
/// for wiring up simple metrics (e.g. a single counter increment or log line) without writing a dedicated class.
/// </summary>
/// <param name="execute">The delegate invoked with each run's <see cref="KyrolusValidationMetricsContext"/>.</param>
/// <example>
/// <code>
/// services.AddSingleton&lt;IKyrolusValidationMetrics&gt;(new KyrolusDelegateValidationMetrics((ctx, ct) =&gt;
/// {
///     logger.LogInformation("Validated {Type} in {Ms}ms, {Count} failures", ctx.RequestType, ctx.Duration.TotalMilliseconds, ctx.Failures.Count);
///     return ValueTask.CompletedTask;
/// }));
/// </code>
/// </example>
public sealed class KyrolusDelegateValidationMetrics(Func<KyrolusValidationMetricsContext, CancellationToken, ValueTask> execute)
    : IKyrolusValidationMetrics
{
    private readonly Func<KyrolusValidationMetricsContext, CancellationToken, ValueTask> execute = execute
        ?? throw new ArgumentNullException(nameof(execute));

    /// <inheritdoc />
    public ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default)
        => execute(context, cancellationToken);
}
