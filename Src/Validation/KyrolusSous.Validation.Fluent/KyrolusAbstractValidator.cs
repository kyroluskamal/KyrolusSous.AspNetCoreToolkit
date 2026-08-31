namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// Base class for building strongly-typed, high-performance fluent validation rules.
/// </summary>
/// <typeparam name="T">The type of the request or model to validate.</typeparam>
/// <example>
/// <code>
/// public class CreateUserValidator : KyrolusAbstractValidator&lt;CreateUserCommand&gt;
/// {
///     public CreateUserValidator()
///     {
///         // Simple property rules
///         RuleFor(x => x.Email)
///             .NotEmpty()
///             .EmailAddress()
///             .WithMessage("Invalid email format.")
///             .WithGroups("UiHints", "Account");
/// 
///         // Scoped RuleSets for specific scenarios
///         RuleSet("Create", () =>
///         {
///             RuleFor(x => x.Password)
///                 .NotEmpty()
///                 .MinLength(8)
///                 .WithMessage("Password must be at least 8 characters.");
/// 
///             RuleFor(x => x.ConfirmPassword)
///                 .Must((confirm, req) => confirm == req.Password)
///                 .WithMessage("Passwords do not match.");
///         });
/// 
///         // Scoped Group tags for categorization
///         Group("Audit", () =>
///         {
///             RuleFor(x => x.CreatedBy)
///                 .NotEmpty()
///                 .WithMessage("CreatedBy audit property is mandatory.");
///         });
///     }
/// }
/// </code>
/// </example>
public abstract class KyrolusAbstractValidator<T> : IKyrolusRequestValidatorWithContext<T>
{
    private readonly List<IKyrolusValidationRule<T>> _rules = [];
    private readonly List<string> _currentRuleSets = [];
    private readonly List<string> _currentGroups = [];

    /// <summary>
    /// Gets or sets the cascade mode across rules in this validator (default: <see cref="KyrolusCascadeMode.Continue"/>).
    /// </summary>
    public KyrolusCascadeMode KyrolusCascadeMode { get; set; } = KyrolusCascadeMode.Continue;

    /// <summary>
    /// Defines a validation rule for a specified property expression.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="expression">The expression selecting the property to validate.</param>
    /// <returns>A rule builder to chain validation predicates and modifiers.</returns>
    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var name = GetPropertyName(expression);
        var rule = new KyrolusPropertyRule<T, TProperty>(name, expression.Compile());
        AttachCurrentScopes(rule);
        _rules.Add(rule);
        return rule;
    }

    /// <summary>
    /// Defines a validation rule for a property using a compiled selector delegate and explicit property name.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">The delegate extracting the property value.</param>
    /// <param name="propertyName">The name of the property for error reporting.</param>
    /// <returns>A rule builder to chain validation predicates and modifiers.</returns>
    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(Func<T, TProperty> selector, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var rule = new KyrolusPropertyRule<T, TProperty>(propertyName, selector);
        AttachCurrentScopes(rule);
        _rules.Add(rule);
        return rule;
    }

    /// <summary>
    /// Defines a validation rule with an inline predicate condition.
    /// </summary>
    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Expression<Func<T, TProperty>> expression,
        Func<TProperty, bool> predicate)
    {
        var ruleBuilder = RuleFor(expression);
        ruleBuilder.Must(predicate);
        return ruleBuilder;
    }

    /// <summary>
    /// Defines a validation rule with an inline predicate condition and custom error message.
    /// </summary>
    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Expression<Func<T, TProperty>> expression,
        Func<TProperty, bool> predicate,
        string defaultMessage)
    {
        var ruleBuilder = RuleFor(expression);
        ruleBuilder.Must(predicate, defaultMessage);
        return ruleBuilder;
    }

    /// <summary>
    /// Defines a validation rule using a selector delegate, explicit property name, and inline predicate.
    /// </summary>
    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Func<T, TProperty> selector,
        string propertyName,
        Func<TProperty, bool> predicate)
    {
        var ruleBuilder = RuleFor(selector, propertyName);
        ruleBuilder.Must(predicate);
        return ruleBuilder;
    }

    /// <summary>
    /// Defines a validation rule using a selector delegate, explicit property name, inline predicate, and custom error message.
    /// </summary>
    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Func<T, TProperty> selector,
        string propertyName,
        Func<TProperty, bool> predicate,
        string defaultMessage)
    {
        var ruleBuilder = RuleFor(selector, propertyName);
        ruleBuilder.Must(predicate, defaultMessage);
        return ruleBuilder;
    }

    /// <summary>
    /// Groups rules defined within the action under a specific RuleSet scenario name.
    /// </summary>
    /// <param name="ruleSetName">The scenario name (e.g. "Create", "Update", "Admin").</param>
    /// <param name="action">The action configuring rules within this RuleSet.</param>
    protected void RuleSet(string ruleSetName, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        RuleSet([ruleSetName], action);
    }

    /// <summary>
    /// Groups rules defined within the action under multiple RuleSet scenario names.
    /// </summary>
    /// <param name="ruleSetNames">Collection of scenario names.</param>
    /// <param name="action">The action configuring rules within these RuleSets.</param>
    protected void RuleSet(IEnumerable<string> ruleSetNames, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var names = ruleSetNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray() ?? [];
        if (names.Length == 0)
        {
            action();
            return;
        }

        _currentRuleSets.AddRange(names);
        try
        {
            action();
        }
        finally
        {
            _currentRuleSets.RemoveAll(name => names.Contains(name, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Groups rules defined within the action under a specific logical Group tag.
    /// </summary>
    /// <param name="groupName">The logical group tag name (e.g. "UiHints", "Security").</param>
    /// <param name="action">The action configuring rules tagged with this Group.</param>
    protected void Group(string groupName, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Group([groupName], action);
    }

    /// <summary>
    /// Groups rules defined within the action under multiple logical Group tags.
    /// </summary>
    /// <param name="groupNames">Collection of logical group tag names.</param>
    /// <param name="action">The action configuring rules tagged with these Groups.</param>
    protected void Group(IEnumerable<string> groupNames, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var names = groupNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray() ?? [];
        if (names.Length == 0)
        {
            action();
            return;
        }

        _currentGroups.AddRange(names);
        try
        {
            action();
        }
        finally
        {
            _currentGroups.RemoveAll(name => names.Contains(name, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Adds a custom validation rule directly to this validator.
    /// </summary>
    protected void AddCustomRule(IKyrolusValidationRule<T> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    private void AttachCurrentScopes<TProperty>(KyrolusPropertyRule<T, TProperty> rule)
    {
        if (_currentRuleSets.Count > 0)
            rule.AttachRuleSets(_currentRuleSets);
        if (_currentGroups.Count > 0)
            rule.AttachGroups(_currentGroups);
    }

    /// <summary>
    /// Validates the request using default context settings.
    /// </summary>
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        T request, CancellationToken cancellationToken = default)
        => ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken);

    /// <summary>
    /// Validates the request using custom context settings (filtering by RuleSets and Groups).
    /// </summary>
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        T request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return [new KyrolusValidationFailure(string.Empty, "Request cannot be null.")];

        var failures = new List<KyrolusValidationFailure>();

        foreach (var rule in _rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ShouldExecuteRule(rule, context))
                continue;

            var ruleFailures = await rule.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (ruleFailures.Count == 0)
                continue;

            var activeRuleSet = ResolveActiveRuleSet(rule, context);
            foreach (var rf in ruleFailures)
                failures.Add(activeRuleSet is not null ? rf with { RuleSet = activeRuleSet } : rf);

            if (KyrolusCascadeMode == KyrolusCascadeMode.Stop)
                break;
        }

        return failures;
    }

    private static string? ResolveActiveRuleSet(IKyrolusValidationRule<T> rule, KyrolusValidationContext context)
    {
        if (context.RuleSets is not { Count: > 0 })
            return null;

        return rule.RuleSets.FirstOrDefault(r => context.RuleSets.Contains(r, StringComparer.OrdinalIgnoreCase))
            ?? (context.RuleSets.Contains("*") ? rule.RuleSets.FirstOrDefault() : context.RuleSets.First());
    }

    private static bool ShouldExecuteRule(IKyrolusValidationRule<T> rule, KyrolusValidationContext context)
    {
        if (!ShouldExecuteScope(context.RuleSets, rule.RuleSets, KyrolusValidationDefaults.DefaultRuleSet))
            return false;

        if (!ShouldExecuteScope(context.Groups, rule.Groups, KyrolusValidationDefaults.DefaultGroup))
            return false;

        return true;
    }

    private static bool ShouldExecuteScope(
        IEnumerable<string>? selectedScopes,
        IEnumerable<string> ruleScopes,
        string defaultScope)
    {
        if (selectedScopes is null || !selectedScopes.Any() || selectedScopes.Contains("*", StringComparer.OrdinalIgnoreCase))
            return true;
        if (!ruleScopes.Any())
            return selectedScopes.Contains(defaultScope, StringComparer.OrdinalIgnoreCase);

        return ruleScopes.Any(ruleScope => selectedScopes.Contains(ruleScope, StringComparer.OrdinalIgnoreCase));
    }

    private static string GetPropertyName<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
            return memberExpression.Member.Name;

        if (expression.Body is UnaryExpression { Operand: MemberExpression operandMember })
            return operandMember.Member.Name;

        return string.Empty;
    }
}
