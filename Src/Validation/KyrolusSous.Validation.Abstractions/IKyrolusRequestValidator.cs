namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Defines a contract for validating a strongly-typed request or model.
/// </summary>
/// <typeparam name="TRequest">The type of the request or entity to validate.</typeparam>
/// <example>
/// <code>
/// public class CreateUserValidator : IKyrolusRequestValidator&lt;CreateUserRequest&gt;
/// {
///     public ValueTask&lt;IReadOnlyList&lt;KyrolusValidationFailure&gt;&gt; ValidateAsync(
///         CreateUserRequest request,
///         CancellationToken cancellationToken = default)
///     {
///         var failures = new List&lt;KyrolusValidationFailure&gt;();
///         if (string.IsNullOrWhiteSpace(request.Email))
///         {
///             failures.Add(new KyrolusValidationFailure(nameof(request.Email), "Email is required.", "ERR_EMAIL_REQUIRED"));
///         }
///         return ValueTask.FromResult&lt;IReadOnlyList&lt;KyrolusValidationFailure&gt;&gt;(failures);
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusRequestValidator<in TRequest>
{
    /// <summary>
    /// Validates the specified request asynchronously.
    /// </summary>
    /// <param name="request">The instance to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of <see cref="KyrolusValidationFailure"/> describing any validation errors, or an empty collection if valid.</returns>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines an advanced contract for validating a request with contextual parameters (e.g., active rule sets, groups, and profiles).
/// </summary>
/// <typeparam name="TRequest">The type of the request or entity to validate.</typeparam>
/// <example>
/// <code>
/// var context = new KyrolusValidationContext(
///     RuleSets: ["Create"],
///     Groups: ["UiHints"],
///     MinimumSeverity: KyrolusValidationSeverity.Warning);
/// 
/// var failures = await validator.ValidateAsync(request, context, cancellationToken);
/// </code>
/// </example>
public interface IKyrolusRequestValidatorWithContext<in TRequest> : IKyrolusRequestValidator<TRequest>
{
    /// <summary>
    /// Validates the specified request within a contextual scope (e.g., filtering by RuleSets, Groups, or Severity).
    /// </summary>
    /// <param name="request">The instance to validate.</param>
    /// <param name="context">The contextual execution options including RuleSets and Groups.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of <see cref="KyrolusValidationFailure"/> representing failures matching the context criteria.</returns>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);
}
