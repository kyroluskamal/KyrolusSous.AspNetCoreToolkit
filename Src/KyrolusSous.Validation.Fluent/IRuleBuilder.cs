using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public interface IRuleBuilder<T, out TProperty>
{
    IRuleBuilder<T, TProperty> WithMessage(string message);
    IRuleBuilder<T, TProperty> WithErrorCode(string errorCode);
    IRuleBuilder<T, TProperty> WithSeverity(KyrolusValidationSeverity severity);
    IRuleBuilder<T, TProperty> When(Func<T, bool> predicate);
    IRuleBuilder<T, TProperty> Unless(Func<T, bool> predicate);
    IRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, string defaultMessage = "Validation failed.");
    IRuleBuilder<T, TProperty> Must(Func<TProperty, T, bool> predicate, string defaultMessage = "Validation failed.");
    IRuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, ValueTask<bool>> predicate, string defaultMessage = "Validation failed.");
    IRuleBuilder<T, TProperty> MustAsync(Func<TProperty, T, CancellationToken, ValueTask<bool>> predicate, string defaultMessage = "Validation failed.");
}
