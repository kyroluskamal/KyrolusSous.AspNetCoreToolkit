namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Defines lifecycle hooks invoked globally before and after validation execution.
/// </summary>
/// <example>
/// <code>
/// public class LoggingValidationHook(ILogger&lt;LoggingValidationHook&gt; logger) : IKyrolusValidationHook
/// {
///     public ValueTask OnBeforeAsync(object? request, KyrolusValidationContext context, CancellationToken ct)
///     {
///         logger.LogInformation("Starting validation for {RequestType}", request?.GetType().Name);
///         return ValueTask.CompletedTask;
///     }
/// 
///     public ValueTask OnAfterAsync(object? request, KyrolusValidationContext context, IReadOnlyList&lt;KyrolusValidationFailure&gt; failures, CancellationToken ct)
///     {
///         if (failures.Count > 0)
///         {
///             logger.LogWarning("Validation failed with {Count} errors", failures.Count);
///         }
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationHook
{
    /// <summary>
    /// Invoked before validation rules are executed.
    /// </summary>
    /// <param name="request">The request being validated.</param>
    /// <param name="context">The validation context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask OnBeforeAsync(
        object? request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked after validation rules are executed and failures are resolved.
    /// </summary>
    /// <param name="request">The request being validated.</param>
    /// <param name="context">The validation context.</param>
    /// <param name="failures">The collection of validation failures produced.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask OnAfterAsync(
        object? request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines strongly-typed lifecycle hooks invoked before and after validation for a specific request type.
/// </summary>
/// <typeparam name="TRequest">The type of the request being validated.</typeparam>
/// <example>
/// <code>
/// public class UserValidationAuditHook : IKyrolusValidationHook&lt;CreateUserRequest&gt;
/// {
///     public ValueTask OnBeforeAsync(CreateUserRequest request, KyrolusValidationContext context, CancellationToken ct)
///         => ValueTask.CompletedTask;
/// 
///     public async ValueTask OnAfterAsync(CreateUserRequest request, KyrolusValidationContext context, IReadOnlyList&lt;KyrolusValidationFailure&gt; failures, CancellationToken ct)
///     {
///         if (failures.Any(f => f.Groups?.Contains("Security") == true))
///         {
///             await NotifySecurityTeamAsync(request, failures, ct);
///         }
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationHook<in TRequest>
{
    /// <summary>
    /// Invoked before validation rules are executed for the specific request type.
    /// </summary>
    ValueTask OnBeforeAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked after validation rules are executed for the specific request type.
    /// </summary>
    ValueTask OnAfterAsync(
        TRequest request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default);
}
