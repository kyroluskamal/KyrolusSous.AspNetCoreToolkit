using System.Linq.Expressions;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public abstract class KyrolusAbstractValidator<T> : IKyrolusRequestValidator<T>
{
    private readonly List<IKyrolusValidationRule<T>> _rules = [];

    public KyrolusCascadeMode KyrolusCascadeMode { get; set; } = KyrolusCascadeMode.Continue;

    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var name = GetPropertyName(expression);
        var rule = new KyrolusPropertyRule<T, TProperty>(name, expression.Compile());
        _rules.Add(rule);
        return rule;
    }

    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(Func<T, TProperty> selector, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var rule = new KyrolusPropertyRule<T, TProperty>(propertyName, selector);
        _rules.Add(rule);
        return rule;
    }

    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Expression<Func<T, TProperty>> expression,
        Func<TProperty, bool> predicate,
        string defaultMessage = "Validation failed.")
    {
        var ruleBuilder = RuleFor(expression);
        ruleBuilder.Must(predicate, defaultMessage);
        return ruleBuilder;
    }

    protected IKyrolusRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Func<T, TProperty> selector,
        string propertyName,
        Func<TProperty, bool> predicate,
        string defaultMessage = "Validation failed.")
    {
        var ruleBuilder = RuleFor(selector, propertyName);
        ruleBuilder.Must(predicate, defaultMessage);
        return ruleBuilder;
    }

    protected void AddCustomRule(IKyrolusValidationRule<T> rule)
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
                if (KyrolusCascadeMode == KyrolusCascadeMode.Stop)
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
