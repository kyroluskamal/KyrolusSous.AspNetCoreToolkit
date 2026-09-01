namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Encapsulates context information passed to distributed tracing collectors (e.g., OpenTelemetry Activity).
/// </summary>
/// <param name="RequestType">The CLR type of the object being validated.</param>
/// <param name="Context">The validation context settings.</param>
public sealed record KyrolusValidationTraceContext(
    Type? RequestType,
    KyrolusValidationContext Context);

/// <summary>
/// Defines a distributed tracing lifecycle listener for tracking validation duration and results with OpenTelemetry/Activities.
/// </summary>
/// <example>
/// <code>
/// public class ActivityValidationTracer : IKyrolusValidationTracer
/// {
///     private static readonly ActivitySource Source = new("Kyrolus.Validation");
/// 
///     public object? Start(KyrolusValidationTraceContext context)
///     {
///         var activity = Source.StartActivity("ValidateRequest");
///         activity?.SetTag("validation.request_type", context.RequestType?.Name);
///         return activity;
///     }
/// 
///     public ValueTask StopAsync(
///         KyrolusValidationTraceContext context,
///         object? state,
///         IReadOnlyList&lt;KyrolusValidationFailure&gt; failures,
///         Exception? exception = null,
///         CancellationToken ct = default)
///     {
///         if (state is Activity activity)
///         {
///             activity.SetTag("validation.failure_count", failures.Count);
///             activity.Dispose();
///         }
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationTracer
{
    /// <summary>
    /// Starts a new tracing span before validation commences.
    /// </summary>
    /// <param name="context">The tracing context details.</param>
    /// <returns>An opaque state object (e.g., <see cref="System.Diagnostics.Activity"/>) to pass into <see cref="StopAsync"/>.</returns>
    object? Start(KyrolusValidationTraceContext context);

    /// <summary>
    /// Completes the tracing span after validation finishes.
    /// </summary>
    /// <param name="context">The same tracing context passed to <see cref="Start"/>.</param>
    /// <param name="state">The opaque state object <see cref="Start"/> returned, or <see langword="null"/> if it returned none.</param>
    /// <param name="failures">The failures produced by validation (empty when it passed).</param>
    /// <param name="exception">The exception that terminated validation, if any; <see langword="null"/> on a normal completion.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    ValueTask StopAsync(
        KyrolusValidationTraceContext context,
        object? state,
        IReadOnlyList<KyrolusValidationFailure> failures,
        Exception? exception = null,
        CancellationToken cancellationToken = default);
}
