namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// Fluent builder contract for configuring validation rules, error messages, error codes, severities, groups, and conditions.
/// </summary>
/// <typeparam name="T">The type of the target model or request being validated.</typeparam>
/// <typeparam name="TProperty">The type of the specific property being validated.</typeparam>
/// <example>
/// <code>
/// RuleFor(x => x.Email)
///     .NotEmpty()
///     .EmailAddress()
///     .WithMessage("Please enter a valid email address.")
///     .WithErrorCode("ERR_EMAIL_INVALID")
///     .WithSeverity(KyrolusValidationSeverity.Error)
///     .WithGroups("UiHints", "Account")
///     .InRuleSet("Create")
///     .When(x => x.IsRegistered);
/// </code>
/// </example>
public interface IKyrolusRuleBuilder<T, out TProperty>
{
    /// <summary>Sets a custom error message for the most recently defined rule step on this property.</summary>
    IKyrolusRuleBuilder<T, TProperty> WithMessage(string message);

    /// <summary>Sets a machine-readable error code across all rule steps for this property.</summary>
    IKyrolusRuleBuilder<T, TProperty> WithErrorCode(string errorCode);

    /// <summary>Sets the severity level across all rule steps for this property.</summary>
    IKyrolusRuleBuilder<T, TProperty> WithSeverity(KyrolusValidationSeverity severity);

    /// <summary>Assigns a single logical group tag to this property's validation rules.</summary>
    IKyrolusRuleBuilder<T, TProperty> WithGroup(string groupName);

    /// <summary>Assigns multiple logical group tags to this property's validation rules.</summary>
    IKyrolusRuleBuilder<T, TProperty> WithGroups(params string[] groupNames);

    /// <summary>Assigns a collection of logical group tags to this property's validation rules.</summary>
    IKyrolusRuleBuilder<T, TProperty> WithGroups(IEnumerable<string> groupNames);

    /// <summary>Assigns this property's validation rules to a specific RuleSet scenario.</summary>
    IKyrolusRuleBuilder<T, TProperty> InRuleSet(string ruleSetName);

    /// <summary>Assigns this property's validation rules to multiple RuleSet scenarios.</summary>
    IKyrolusRuleBuilder<T, TProperty> InRuleSets(params string[] ruleSetNames);

    /// <summary>Assigns this property's validation rules to a collection of RuleSet scenarios.</summary>
    IKyrolusRuleBuilder<T, TProperty> InRuleSets(IEnumerable<string> ruleSetNames);

    /// <summary>Defines a condition that must evaluate to true for this rule to be executed.</summary>
    IKyrolusRuleBuilder<T, TProperty> When(Func<T, bool> predicate);

    /// <summary>Defines a condition that must evaluate to false for this rule to be executed (skip if true).</summary>
    IKyrolusRuleBuilder<T, TProperty> Unless(Func<T, bool> predicate);

    /// <summary>Adds a synchronous validation predicate taking the property value.</summary>
    IKyrolusRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate);

    /// <summary>Adds a synchronous validation predicate taking both property value and root instance.</summary>
    IKyrolusRuleBuilder<T, TProperty> Must(Func<TProperty, T, bool> predicate);

    /// <summary>Adds an asynchronous validation predicate taking property value and cancellation token.</summary>
    IKyrolusRuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, ValueTask<bool>> predicate);

    /// <summary>Adds an asynchronous validation predicate taking property value, root instance, and cancellation token.</summary>
    IKyrolusRuleBuilder<T, TProperty> MustAsync(Func<TProperty, T, CancellationToken, ValueTask<bool>> predicate);
}
