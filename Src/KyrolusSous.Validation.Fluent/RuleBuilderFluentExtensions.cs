using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public static class RuleBuilderFluentExtensions
{
    public static IRuleBuilder<T, string?> NotEmpty<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsNotEmpty(val), "Property must not be empty.");

    public static IRuleBuilder<T, string?> Empty<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsEmpty(val), "Property must be empty.");

    public static IRuleBuilder<T, TProperty> NotNull<T, TProperty>(this IRuleBuilder<T, TProperty> builder)
        => builder.Must(val => RuleBuilderExtensions.IsNotNull(val), "Property must not be null.");

    public static IRuleBuilder<T, TProperty> Null<T, TProperty>(this IRuleBuilder<T, TProperty> builder)
        => builder.Must(val => RuleBuilderExtensions.IsNull(val), "Property must be null.");

    public static IRuleBuilder<T, TProperty> Equal<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty expected)
        => builder.Must(val => RuleBuilderExtensions.IsEqual(val, expected), $"Must be equal to {expected}.");

    public static IRuleBuilder<T, TProperty> NotEqual<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty expected)
        => builder.Must(val => RuleBuilderExtensions.IsNotEqual(val, expected), $"Must not be equal to {expected}.");

    public static IRuleBuilder<T, string?> Length<T>(this IRuleBuilder<T, string?> builder, int min, int max)
        => builder.Must(val => RuleBuilderExtensions.IsLengthValid(val, min, max), $"Length must be between {min} and {max}.");

    public static IRuleBuilder<T, string?> MinLength<T>(this IRuleBuilder<T, string?> builder, int min)
        => builder.Must(val => RuleBuilderExtensions.IsMinLengthValid(val, min), $"Length must be at least {min}.");

    public static IRuleBuilder<T, string?> MaxLength<T>(this IRuleBuilder<T, string?> builder, int max)
        => builder.Must(val => RuleBuilderExtensions.IsMaxLengthValid(val, max), $"Length must be at most {max}.");

    public static IRuleBuilder<T, string?> ExactLength<T>(this IRuleBuilder<T, string?> builder, int length)
        => builder.Must(val => RuleBuilderExtensions.IsExactLengthValid(val, length), $"Length must be exactly {length}.");

    public static IRuleBuilder<T, TEnum> IsInEnum<T, TEnum>(this IRuleBuilder<T, TEnum> builder) where TEnum : struct, Enum
        => builder.Must(val => RuleBuilderExtensions.IsInEnumValid(val), "Value is not a valid enum member.");

    public static IRuleBuilder<T, decimal> ScalePrecision<T>(this IRuleBuilder<T, decimal> builder, int precision, int scale)
        => builder.Must(val => RuleBuilderExtensions.IsScalePrecisionValid(val, precision, scale), $"Precision must not exceed {precision} digits with {scale} decimals.");

    public static IRuleBuilder<T, TProperty> GreaterThan<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsGreaterThan(val, limit), $"Must be greater than {limit}.");

    public static IRuleBuilder<T, TProperty> GreaterThanOrEqualTo<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsGreaterThanOrEqualTo(val, limit), $"Must be greater than or equal to {limit}.");

    public static IRuleBuilder<T, TProperty> LessThan<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsLessThan(val, limit), $"Must be less than {limit}.");

    public static IRuleBuilder<T, TProperty> LessThanOrEqualTo<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsLessThanOrEqualTo(val, limit), $"Must be less than or equal to {limit}.");

    public static IRuleBuilder<T, TProperty> InclusiveBetween<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty from, TProperty to) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsInclusiveBetween(val, from, to), $"Must be between {from} and {to}.");

    public static IRuleBuilder<T, TProperty> ExclusiveBetween<T, TProperty>(this IRuleBuilder<T, TProperty> builder, TProperty from, TProperty to) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsExclusiveBetween(val, from, to), $"Must be exclusively between {from} and {to}.");

    public static IRuleBuilder<T, string?> EmailAddress<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsEmailAddress(val), "Invalid email address format.");

    public static IRuleBuilder<T, string?> IsEmail<T>(this IRuleBuilder<T, string?> builder)
        => builder.EmailAddress();

    public static IRuleBuilder<T, string?> Matches<T>(this IRuleBuilder<T, string?> builder, string regexPattern)
        => builder.Must(val => RuleBuilderExtensions.IsRegexMatch(val, regexPattern), "Format does not match required pattern.");

    public static IRuleBuilder<T, string?> CreditCard<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsCreditCardValid(val), "Invalid credit card number.");

    public static IRuleBuilder<T, string?> NationalId<T>(this IRuleBuilder<T, string?> builder, string countryCode = "EG")
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsNationalIdValid(val, countryCode), "Invalid National ID.");

    public static IRuleBuilder<T, string?> IbanValid<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsIbanValid(val), "Invalid IBAN number.");

    public static IRuleBuilder<T, string?> StrongPassword<T>(this IRuleBuilder<T, string?> builder, PasswordOptions? options = null)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsStrongPasswordValid(val, options), "Password does not meet complexity requirements.");

    public static IRuleBuilder<T, string?> JsonValid<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsJsonValid(val), "Invalid JSON format.");

    public static IRuleBuilder<T, string?> Base64Valid<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsBase64Valid(val), "Invalid Base64 format.");

    public static IRuleBuilder<T, string?> CronExpressionValid<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsCronExpressionValid(val), "Invalid Cron expression.");

    public static IRuleBuilder<T, string?> MacAddressValid<T>(this IRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsMacAddressValid(val), "Invalid MAC Address.");

    public static IRuleBuilder<T, TProperty> SetValidator<T, TProperty>(
        this IRuleBuilder<T, TProperty> builder,
        IKyrolusRequestValidator<TProperty> childValidator)
    {
        ArgumentNullException.ThrowIfNull(childValidator);
        return builder.MustAsync(async (val, ct) =>
        {
            if (val is null) return true;
            var failures = await childValidator.ValidateAsync(val, ct).ConfigureAwait(false);
            return failures.Count == 0;
        }, "Child validation failed.");
    }
}
