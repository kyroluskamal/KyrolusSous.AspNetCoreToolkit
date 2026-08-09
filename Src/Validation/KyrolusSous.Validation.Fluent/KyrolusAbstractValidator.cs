using System.Linq.Expressions;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public abstract class KyrolusAbstractValidator<T> : IKyrolusRequestValidator<T>
{
    private readonly List<IValidationRule<T>> _rules = [];

    public CascadeMode CascadeMode { get; set; } = CascadeMode.Continue;

    protected IRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var name = GetPropertyName(expression);
        var rule = new PropertyRule<T, TProperty>(name, expression.Compile());
        _rules.Add(rule);
        return rule;
    }

    protected IRuleBuilder<T, TProperty> RuleFor<TProperty>(Func<T, TProperty> selector, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var rule = new PropertyRule<T, TProperty>(propertyName, selector);
        _rules.Add(rule);
        return rule;
    }

    protected IRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Expression<Func<T, TProperty>> expression,
        Func<TProperty, bool> predicate,
        string defaultMessage = "Validation failed.")
    {
        var ruleBuilder = RuleFor(expression);
        ruleBuilder.Must(predicate, defaultMessage);
        return ruleBuilder;
    }

    protected IRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Func<T, TProperty> selector,
        string propertyName,
        Func<TProperty, bool> predicate,
        string defaultMessage = "Validation failed.")
    {
        var ruleBuilder = RuleFor(selector, propertyName);
        ruleBuilder.Must(predicate, defaultMessage);
        return ruleBuilder;
    }

    protected void AddCustomRule(IValidationRule<T> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        T request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return [new KyrolusValidationFailure(string.Empty, "Request cannot be null.")];
        }

        var failures = new List<KyrolusValidationFailure>();

        foreach (var rule in _rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ruleFailures = await rule.ValidateAsync(request, cancellationToken).ConfigureAwait(false);

            if (ruleFailures.Count > 0)
            {
                failures.AddRange(ruleFailures);
                if (CascadeMode == CascadeMode.Stop)
                {
                    break;
                }
            }
        }

        return failures;
    }

    private static string GetPropertyName<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression operandMember })
        {
            return operandMember.Member.Name;
        }

        return string.Empty;
    }
}
