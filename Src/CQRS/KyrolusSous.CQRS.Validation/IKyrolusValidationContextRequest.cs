namespace KyrolusSous.CQRS.Validation;

/// <summary>
/// Opt-in marker letting a single request supply its own <see cref="KyrolusValidationContext"/>
/// (active RuleSets, Group filters, Profiles, and/or a minimum severity) to
/// <see cref="KyrolusValidationBehavior{TRequest,TResponse}"/>, the same way
/// <c>IKyrolusCacheableRequest</c>/<c>IKyrolusTenantScopedRequest</c> opt a request into their own
/// respective pipeline behaviors elsewhere in this codebase.
/// </summary>
/// <remarks>
/// Without this, <see cref="KyrolusValidationBehavior{TRequest,TResponse}"/> could only ever call the
/// engine's/validator's context-FREE overloads (<c>ValidateAsync(request, cancellationToken)</c>),
/// even though <see cref="IKyrolusValidationEngine"/> and
/// <see cref="IKyrolusRequestValidatorWithContext{TRequest}"/> both already expose a
/// <see cref="KyrolusValidationContext"/>-aware overload built specifically for scoping validation to a
/// named RuleSet ("Create" vs. "Update"), filtering by Group tags, applying a registered Profile, or
/// tightening/loosening the blocking severity threshold per request - none of which was ever reachable
/// from the CQRS pipeline before this marker existed.
/// <para>
/// Purely additive: a request that does not implement this interface (or implements it but returns a
/// <see langword="null"/> <see cref="ValidationContext"/>) behaves exactly as before, calling the
/// context-free overloads unchanged.
/// </para>
/// </remarks>
public interface IKyrolusValidationContextRequest
{
    /// <summary>
    /// The validation context this request should be validated under, or <see langword="null"/> to
    /// fall back to the default (context-free) validation path.
    /// </summary>
    KyrolusValidationContext? ValidationContext { get; }
}
