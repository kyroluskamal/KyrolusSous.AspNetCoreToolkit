namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// Default implementation of <see cref="IKyrolusRuleBuilder{T, TProperty}"/> and <see cref="IKyrolusValidationRule{T}"/>
/// representing an executable property validation rule chain.
/// </summary>
/// <typeparam name="T">The type of the model being validated.</typeparam>
/// <typeparam name="TProperty">The type of the property being validated.</typeparam>
public class KyrolusPropertyRule<T, TProperty>(string propertyName, Func<T, TProperty> propertySelector)
    : IKyrolusRuleBuilder<T, TProperty>, IKyrolusValidationRule<T>
{
    private sealed class RuleStep(Func<TProperty, T, CancellationToken, ValueTask<bool>> predicate)
    {
        public Func<TProperty, T, CancellationToken, ValueTask<bool>> Predicate { get; } = predicate;
        public string? CustomMessage { get; set; }
        public string? ErrorCode { get; set; }
        public KyrolusValidationSeverity Severity { get; set; } = KyrolusValidationSeverity.Error;
    }

    private readonly Func<T, TProperty> _propertySelector = propertySelector ?? throw new ArgumentNullException(nameof(propertySelector));
    private readonly List<RuleStep> _steps = [];
    private readonly HashSet<string> _ruleSets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _groups = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the name of the property being validated.</summary>
    public string PropertyName { get; } = propertyName ?? string.Empty;

    /// <summary>Gets the execution condition predicate (if any).</summary>
    public Func<T, bool>? WhenPredicate { get; private set; }

    /// <summary>Gets the negative execution condition predicate (if any).</summary>
    public Func<T, bool>? UnlessPredicate { get; private set; }

    /// <summary>Gets the RuleSets this property rule belongs to.</summary>
    public IReadOnlyCollection<string> RuleSets => _ruleSets;

    /// <summary>Gets the Group tags assigned to this property rule.</summary>
    public IReadOnlyCollection<string> Groups => _groups;

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _steps.Add(new RuleStep((val, _, _) => ValueTask.FromResult(predicate(val))));
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> Must(Func<TProperty, T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _steps.Add(new RuleStep((val, req, _) => ValueTask.FromResult(predicate(val, req))));
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, ValueTask<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _steps.Add(new RuleStep((val, _, ct) => predicate(val, ct)));
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> MustAsync(Func<TProperty, T, CancellationToken, ValueTask<bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _steps.Add(new RuleStep((val, req, ct) => predicate(val, req, ct)));
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> WithMessage(string message)
    {
        if (_steps.Count > 0)
            _steps[^1].CustomMessage = message;
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> WithErrorCode(string errorCode)
    {
        foreach (var step in _steps)
            step.ErrorCode = errorCode;
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> WithSeverity(KyrolusValidationSeverity severity)
    {
        foreach (var step in _steps)
            step.Severity = severity;
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> WithGroup(string groupName)
    {
        if (!string.IsNullOrWhiteSpace(groupName))
            _groups.Add(groupName);
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> WithGroups(params string[] groupNames)
        => WithGroups((IEnumerable<string>)groupNames);

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> WithGroups(IEnumerable<string> groupNames)
    {
        if (groupNames is not null)
            foreach (var name in groupNames) WithGroup(name);
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> InRuleSet(string ruleSetName)
    {
        if (!string.IsNullOrWhiteSpace(ruleSetName))
            _ruleSets.Add(ruleSetName);
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> InRuleSets(params string[] ruleSetNames)
        => InRuleSets((IEnumerable<string>)ruleSetNames);

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> InRuleSets(IEnumerable<string> ruleSetNames)
    {
        if (ruleSetNames is not null)
            foreach (var name in ruleSetNames) InRuleSet(name);
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> When(Func<T, bool> predicate)
    {
        WhenPredicate = predicate;
        return this;
    }

    /// <inheritdoc />
    public IKyrolusRuleBuilder<T, TProperty> Unless(Func<T, bool> predicate)
    {
        UnlessPredicate = predicate;
        return this;
    }

    internal void AttachRuleSets(IEnumerable<string> ruleSets)
    {
        InRuleSets(ruleSets);
    }

    internal void AttachGroups(IEnumerable<string> groups)
    {
        WithGroups(groups);
    }

    private bool ShouldSkipValidation(T request)
        => request is null || (WhenPredicate is not null && !WhenPredicate(request)) || (UnlessPredicate is not null && UnlessPredicate(request));


    private KyrolusValidationFailure CreateFailure(TProperty propValue, RuleStep step)
    {
        var message = step.CustomMessage
            ?? (!string.IsNullOrWhiteSpace(PropertyName)
                ? $"The specified condition was not met for '{PropertyName}'."
                : KyrolusValidationDefaults.DefaultErrorMessage);

        return new KyrolusValidationFailure(
            PropertyName: PropertyName,
            ErrorMessage: message,
            ErrorCode: step.ErrorCode,
            Severity: step.Severity,
            RuleSet: null,
            MessageKey: string.IsNullOrWhiteSpace(step.ErrorCode) ? null : step.ErrorCode,
            AttemptedValue: propValue,
            Groups: _groups.Count > 0 ? [.. _groups] : null);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(T request, CancellationToken cancellationToken = default)
    {
        if (ShouldSkipValidation(request)) return [];

        var propValue = _propertySelector(request);
        var failures = new List<KyrolusValidationFailure>();

        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isValid = await step.Predicate(propValue, request, cancellationToken).ConfigureAwait(false);
            if (isValid) continue;

            failures.Add(CreateFailure(propValue, step));
            break;
        }

        return failures;
    }
}
