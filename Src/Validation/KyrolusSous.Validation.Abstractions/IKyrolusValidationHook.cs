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
    /// Where this hook runs relative to other global hooks: lower values run first for
    /// <see cref="OnBeforeAsync"/> and, since <c>OnAfterAsync</c> is invoked in the same relative order (it is
    /// not a LIFO unwind), also first for <see cref="OnAfterAsync"/>. Hooks that don't override this share the
    /// default of <c>0</c> and then run in registration order, so most hooks never need to set it. A
    /// <see cref="KyrolusValidationHookOrderAttribute"/> read by <c>KyrolusSous.Validation.Generator</c>, when
    /// present, takes precedence over this property without requiring a source change.
    /// </summary>
    int Order => 0;

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
    /// Where this hook runs relative to other hooks registered for <typeparamref name="TRequest"/>. See
    /// <see cref="IKyrolusValidationHook.Order"/> for the full semantics - they're identical here.
    /// </summary>
    int Order => 0;

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
