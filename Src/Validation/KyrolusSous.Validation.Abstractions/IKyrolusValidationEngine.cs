namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Defines the central execution engine responsible for coordinating validation workflows, caching,
/// tracing, metrics, hooks, profiles, and composite entity validations.
/// </summary>
/// <example>
/// <code>
/// // Inject engine from DI
/// public class OrderService(IKyrolusValidationEngine validationEngine)
/// {
///     public async Task CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
///     {
///         // Execute validation with automatic caching and hooks
///         var failures = await validationEngine.ValidateAsync(command, ct);
///         if (failures.Count > 0)
///         {
///             throw new KyrolusValidationException(failures);
///         }
/// 
///         // Or validate with specific profiles / rule sets
///         var updateContext = new KyrolusValidationContext(Profiles: [KyrolusValidationProfiles.Update.Name]);
///         var updateFailures = await validationEngine.ValidateAsync(command, updateContext, ct);
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationEngine
{
    /// <summary>
    /// Validates a single request instance using default validation context settings.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request being validated.</typeparam>
    /// <param name="request">The request instance to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of <see cref="KyrolusValidationFailure"/>.</returns>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a single request instance using a custom <see cref="KyrolusValidationContext"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request being validated.</typeparam>
    /// <param name="request">The request instance to validate.</param>
    /// <param name="context">The contextual settings controlling rule sets, groups, profiles, and severity filters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of filtered and mapped <see cref="KyrolusValidationFailure"/>.</returns>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a composite composed of two distinct models/requests simultaneously.
    /// </summary>
    /// <example>
    /// <code>
    /// var failures = await engine.ValidateCompositeAsync(customerModel, orderModel, ct);
    /// </code>
    /// </example>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a composite composed of two distinct models/requests within a specific context.
    /// </summary>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a composite composed of three distinct models/requests simultaneously.
    /// </summary>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a composite composed of three distinct models/requests within a specific context.
    /// </summary>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a composite composed of four distinct models/requests simultaneously.
    /// </summary>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird, TFourth>(
        TFirst first,
        TSecond second,
        TThird third,
        TFourth fourth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a composite composed of four distinct models/requests within a specific context.
    /// </summary>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird, TFourth>(
        TFirst first,
        TSecond second,
        TThird third,
        TFourth fourth,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);
}
