namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// Provides high-level fluent validation extension methods for common data types, format validations, and nested validators.
/// </summary>
public static class RuleBuilderFluentExtensions
{
    /// <summary>Ensures that a string property is not null, empty, or whitespace.</summary>
    public static IKyrolusRuleBuilder<T, string?> NotEmpty<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsNotEmpty(val), "Property must not be empty.");

    /// <summary>Ensures that a string property is null, empty, or whitespace.</summary>
    public static IKyrolusRuleBuilder<T, string?> Empty<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsEmpty(val), "Property must be empty.");

    /// <summary>Ensures that a property is not null.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> NotNull<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder)
        => builder.Must(val => RuleBuilderExtensions.IsNotNull(val), "Property must not be null.");

    /// <summary>Ensures that a property is null.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> Null<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder)
        => builder.Must(val => RuleBuilderExtensions.IsNull(val), "Property must be null.");

    /// <summary>Ensures that a property equals the specified expected value.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> Equal<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty expected)
        => builder.Must(val => RuleBuilderExtensions.IsEqual(val, expected), $"Must be equal to {expected}.");

    /// <summary>Ensures that a property does not equal the specified value.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> NotEqual<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty expected)
        => builder.Must(val => RuleBuilderExtensions.IsNotEqual(val, expected), $"Must not be equal to {expected}.");

    /// <summary>Ensures that a string length is between the specified min and max (inclusive).</summary>
    public static IKyrolusRuleBuilder<T, string?> Length<T>(this IKyrolusRuleBuilder<T, string?> builder, int min, int max)
        => builder.Must(val => RuleBuilderExtensions.IsLengthValid(val, min, max), $"Length must be between {min} and {max}.");

    /// <summary>Ensures that a string length is at least the specified minimum.</summary>
    public static IKyrolusRuleBuilder<T, string?> MinLength<T>(this IKyrolusRuleBuilder<T, string?> builder, int min)
        => builder.Must(val => RuleBuilderExtensions.IsMinLengthValid(val, min), $"Length must be at least {min}.");

    /// <summary>Ensures that a string length does not exceed the specified maximum.</summary>
    public static IKyrolusRuleBuilder<T, string?> MaxLength<T>(this IKyrolusRuleBuilder<T, string?> builder, int max)
        => builder.Must(val => RuleBuilderExtensions.IsMaxLengthValid(val, max), $"Length must be at most {max}.");

    /// <summary>Ensures that a string length exactly matches the specified length.</summary>
    public static IKyrolusRuleBuilder<T, string?> ExactLength<T>(this IKyrolusRuleBuilder<T, string?> builder, int length)
        => builder.Must(val => RuleBuilderExtensions.IsExactLengthValid(val, length), $"Length must be exactly {length}.");

    /// <summary>Ensures that the enum property value is a defined member of the enum.</summary>
    public static IKyrolusRuleBuilder<T, TEnum> IsInEnum<T, TEnum>(this IKyrolusRuleBuilder<T, TEnum> builder) where TEnum : struct, Enum
        => builder.Must(val => RuleBuilderExtensions.IsInEnumValid(val), "Value is not a valid enum member.");

    /// <summary>Ensures that a decimal value has at most precision total digits and scale decimal digits.</summary>
    public static IKyrolusRuleBuilder<T, decimal> ScalePrecision<T>(this IKyrolusRuleBuilder<T, decimal> builder, int precision, int scale)
        => builder.Must(val => RuleBuilderExtensions.IsScalePrecisionValid(val, precision, scale), $"Precision must not exceed {precision} digits with {scale} decimals.");

    /// <summary>Ensures that a comparable value is strictly greater than the limit.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> GreaterThan<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsGreaterThan(val, limit), $"Must be greater than {limit}.");

    /// <summary>Ensures that a comparable value is greater than or equal to the limit.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> GreaterThanOrEqualTo<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsGreaterThanOrEqualTo(val, limit), $"Must be greater than or equal to {limit}.");

    /// <summary>Ensures that a comparable value is strictly less than the limit.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> LessThan<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsLessThan(val, limit), $"Must be less than {limit}.");

    /// <summary>Ensures that a comparable value is less than or equal to the limit.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> LessThanOrEqualTo<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty limit) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsLessThanOrEqualTo(val, limit), $"Must be less than or equal to {limit}.");

    /// <summary>Ensures that a comparable value is inclusively between from and to.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> InclusiveBetween<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty from, TProperty to) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsInclusiveBetween(val, from, to), $"Must be between {from} and {to}.");

    /// <summary>Ensures that a comparable value is exclusively between from and to.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> ExclusiveBetween<T, TProperty>(this IKyrolusRuleBuilder<T, TProperty> builder, TProperty from, TProperty to) where TProperty : IComparable<TProperty>
        => builder.Must(val => RuleBuilderExtensions.IsExclusiveBetween(val, from, to), $"Must be exclusively between {from} and {to}.");

    /// <summary>Ensures that the string property is a valid email address.</summary>
    public static IKyrolusRuleBuilder<T, string?> EmailAddress<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsEmailAddress(val), "Invalid email address format.");

    /// <summary>Alias for <see cref="EmailAddress{T}"/>.</summary>
    public static IKyrolusRuleBuilder<T, string?> IsEmail<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.EmailAddress();

    /// <summary>Ensures that the string property matches the provided regular expression pattern.</summary>
    public static IKyrolusRuleBuilder<T, string?> Matches<T>(this IKyrolusRuleBuilder<T, string?> builder, string regexPattern)
        => builder.Must(val => RuleBuilderExtensions.IsRegexMatch(val, regexPattern), "Format does not match required pattern.");

    /// <summary>Validates that the string property is a valid credit card number (Luhn algorithm check).</summary>
    public static IKyrolusRuleBuilder<T, string?> CreditCard<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => RuleBuilderExtensions.IsCreditCardValid(val), "Invalid credit card number.");

    /// <summary>Validates that the string property is a valid National ID for the specified country (default: "EG").</summary>
    public static IKyrolusRuleBuilder<T, string?> NationalId<T>(this IKyrolusRuleBuilder<T, string?> builder, string countryCode = "EG")
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsNationalIdValid(val, countryCode), "Invalid National ID.");

    /// <summary>Validates that the string property is a valid Spanish DNI.</summary>
    public static IKyrolusRuleBuilder<T, string?> SpanishDni<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsSpanishDniValid(val), "Invalid Spanish DNI.");

    /// <summary>Validates that the string property is a valid Spanish NIE.</summary>
    public static IKyrolusRuleBuilder<T, string?> SpanishNie<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsSpanishNieValid(val), "Invalid Spanish NIE.");

    /// <summary>Validates that the string property is a valid Spanish CIF.</summary>
    public static IKyrolusRuleBuilder<T, string?> SpanishCif<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsSpanishCifValid(val), "Invalid Spanish CIF.");

    /// <summary>Validates that the string property is a valid Spanish NIF.</summary>
    public static IKyrolusRuleBuilder<T, string?> SpanishNif<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsSpanishNifValid(val), "Invalid Spanish NIF.");

    /// <summary>Validates that the string property is a valid IBAN number.</summary>
    public static IKyrolusRuleBuilder<T, string?> IbanValid<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsIbanValid(val), "Invalid IBAN number.");

    /// <summary>Validates that the string property meets strong password complexity criteria.</summary>
    public static IKyrolusRuleBuilder<T, string?> StrongPassword<T>(this IKyrolusRuleBuilder<T, string?> builder, KyrolusPasswordOptions? options = null)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsStrongPasswordValid(val, options), "Password does not meet complexity requirements.");

    /// <summary>Validates that the string property contains syntactically valid JSON.</summary>
    public static IKyrolusRuleBuilder<T, string?> JsonValid<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsJsonValid(val), "Invalid JSON format.");

    /// <summary>Validates that the string property contains valid Base64 encoded data.</summary>
    public static IKyrolusRuleBuilder<T, string?> Base64Valid<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsBase64Valid(val), "Invalid Base64 format.");

    /// <summary>Validates that the string property contains a valid Cron expression.</summary>
    public static IKyrolusRuleBuilder<T, string?> CronExpressionValid<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsCronExpressionValid(val), "Invalid Cron expression.");

    /// <summary>Validates that the string property contains a valid MAC Address.</summary>
    public static IKyrolusRuleBuilder<T, string?> MacAddressValid<T>(this IKyrolusRuleBuilder<T, string?> builder)
        => builder.Must(val => AdvancedRuleBuilderExtensions.IsMacAddressValid(val), "Invalid MAC Address.");

    /// <summary>Executes a child validator asynchronously against a nested complex property object.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> SetValidator<T, TProperty>(
        this IKyrolusRuleBuilder<T, TProperty> builder,
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

    /// <summary>Adds a synchronous predicate with an explicit custom failure message.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> Must<T, TProperty>(
        this IKyrolusRuleBuilder<T, TProperty> builder,
        Func<TProperty, bool> predicate,
        string message)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);
        return builder.Must(predicate).WithMessage(message);
    }

    /// <summary>Adds a synchronous predicate taking model and property with an explicit custom failure message.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> Must<T, TProperty>(
        this IKyrolusRuleBuilder<T, TProperty> builder,
        Func<TProperty, T, bool> predicate,
        string message)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);
        return builder.Must(predicate).WithMessage(message);
    }

    /// <summary>Adds an asynchronous predicate with an explicit custom failure message.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> MustAsync<T, TProperty>(
        this IKyrolusRuleBuilder<T, TProperty> builder,
        Func<TProperty, CancellationToken, ValueTask<bool>> predicate,
        string message)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);
        return builder.MustAsync(predicate).WithMessage(message);
    }

    /// <summary>Adds an asynchronous predicate taking model and property with an explicit custom failure message.</summary>
    public static IKyrolusRuleBuilder<T, TProperty> MustAsync<T, TProperty>(
        this IKyrolusRuleBuilder<T, TProperty> builder,
        Func<TProperty, T, CancellationToken, ValueTask<bool>> predicate,
        string message)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);
        return builder.MustAsync(predicate).WithMessage(message);
    }
}
