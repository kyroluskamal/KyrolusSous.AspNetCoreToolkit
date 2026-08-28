using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public interface IKyrolusRuleBuilder<T, out TProperty>
{
    IKyrolusRuleBuilder<T, TProperty> WithMessage(string message);
    IKyrolusRuleBuilder<T, TProperty> WithErrorCode(string errorCode);
    IKyrolusRuleBuilder<T, TProperty> WithSeverity(KyrolusValidationSeverity severity);
    IKyrolusRuleBuilder<T, TProperty> When(Func<T, bool> predicate);
    IKyrolusRuleBuilder<T, TProperty> Unless(Func<T, bool> predicate);
    IKyrolusRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, string defaultMessage = "Validation failed.");
    IKyrolusRuleBuilder<T, TProperty> Must(Func<TProperty, T, bool> predicate, string defaultMessage = "Validation failed.");
    IKyrolusRuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, ValueTask<bool>> predicate, string defaultMessage = "Validation failed.");
    IKyrolusRuleBuilder<T, TProperty> MustAsync(Func<TProperty, T, CancellationToken, ValueTask<bool>> predicate, string defaultMessage = "Validation failed.");
}
