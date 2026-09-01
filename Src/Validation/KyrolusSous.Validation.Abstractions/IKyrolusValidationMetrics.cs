namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Encapsulates execution timing and diagnostic statistics for a completed validation run.
/// </summary>
/// <param name="RequestType">The CLR type of the validated request.</param>
/// <param name="Context">The context settings used during execution.</param>
/// <param name="Failures">The list of failures produced.</param>
/// <param name="Duration">The total elapsed time taken by the validation engine.</param>
public sealed record KyrolusValidationMetricsContext(
    Type? RequestType,
    KyrolusValidationContext Context,
    IReadOnlyList<KyrolusValidationFailure> Failures,
    TimeSpan Duration);

/// <summary>
/// Defines a contract for recording performance and failure metrics (e.g., Prometheus, OpenTelemetry).
/// </summary>
/// <example>
/// <code>
/// public class OpenTelemetryValidationMetrics(Meter meter) : IKyrolusValidationMetrics
/// {
///     private readonly Counter&lt;long&gt; _failureCounter = meter.CreateCounter&lt;long&gt;("validation_failures_total");
/// 
///     public ValueTask RecordAsync(KyrolusValidationMetricsContext context, CancellationToken ct)
///     {
///         if (context.Failures.Count > 0)
///         {
///             _failureCounter.Add(context.Failures.Count, new KeyValuePair&lt;string, object?&gt;("type", context.RequestType?.Name));
///         }
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationMetrics
{
    /// <summary>
    /// Records validation execution metrics asynchronously.
    /// </summary>
    /// <param name="context">The completed run's request type, context, failures, and elapsed duration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default);
}
