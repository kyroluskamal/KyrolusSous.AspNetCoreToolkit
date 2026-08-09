using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public sealed class PropertyRule<T, TProperty> : IRuleBuilder<T, TProperty>, IValidationRule<T>
{
    private sealed class RuleStep
    {
        public Func<TProperty, T, bool> Predicate { get; }
        public string DefaultMessage { get; }
        public string? CustomMessage { get; set; }
        public string? ErrorCode { get; set; }
        public KyrolusValidationSeverity Severity { get; set; } = KyrolusValidationSeverity.Error;

        public RuleStep(Func<TProperty, T, bool> predicate, string defaultMessage)
        {
            Predicate = predicate;
            DefaultMessage = defaultMessage;
        }
    }

    private readonly Func<T, TProperty> _propertySelector;
    private readonly List<RuleStep> _steps = [];

    public string PropertyName { get; }
    public Func<T, bool>? WhenPredicate { get; private set; }
    public Func<T, bool>? UnlessPredicate { get; private set; }

    public PropertyRule(string propertyName, Func<T, TProperty> propertySelector)
    {
        PropertyName = propertyName ?? string.Empty;
        _propertySelector = propertySelector ?? throw new ArgumentNullException(nameof(propertySelector));
    }

    public IRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, string defaultMessage = "Validation failed.")
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _steps.Add(new RuleStep((val, _) => predicate(val), defaultMessage));
        return this;
    }

    public IRuleBuilder<T, TProperty> Must(Func<TProperty, T, bool> predicate, string defaultMessage = "Validation failed.")
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _steps.Add(new RuleStep(predicate, defaultMessage));
        return this;
    }

    public IRuleBuilder<T, TProperty> WithMessage(string message)
    {
        foreach (var step in _steps)
        {
            step.CustomMessage = message;
        }
        return this;
    }

    public IRuleBuilder<T, TProperty> WithErrorCode(string errorCode)
    {
        foreach (var step in _steps)
        {
            step.ErrorCode = errorCode;
        }
        return this;
    }

    public IRuleBuilder<T, TProperty> WithSeverity(KyrolusValidationSeverity severity)
    {
        foreach (var step in _steps)
        {
            step.Severity = severity;
        }
        return this;
    }

    public IRuleBuilder<T, TProperty> WithSeverity(string severity)
    {
        if (Enum.TryParse<KyrolusValidationSeverity>(severity, true, out var result))
        {
            foreach (var step in _steps)
            {
                step.Severity = result;
            }
        }
        return this;
    }

    public IRuleBuilder<T, TProperty> When(Func<T, bool> predicate)
    {
        WhenPredicate = predicate;
        return this;
    }

    public IRuleBuilder<T, TProperty> Unless(Func<T, bool> predicate)
    {
        UnlessPredicate = predicate;
        return this;
    }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(T request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
        }

        if (WhenPredicate is not null && !WhenPredicate(request))
        {
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
        }

        if (UnlessPredicate is not null && UnlessPredicate(request))
        {
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
        }

        var propValue = _propertySelector(request);
        var failures = new List<KyrolusValidationFailure>();

        foreach (var step in _steps)
        {
            var isValid = step.Predicate(propValue, request);
            if (!isValid)
            {
                var message = step.CustomMessage ?? step.DefaultMessage;
                failures.Add(new KyrolusValidationFailure(
                    PropertyName: PropertyName,
                    ErrorMessage: message,
                    ErrorCode: step.ErrorCode,
                    Severity: step.Severity,
                    AttemptedValue: propValue));

                break; // Stop evaluating further rules on this property once it fails
            }
        }

        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(failures);
    }
}
