using System.Linq.Expressions;
using FluentValidation;
using KyrolusSous.Validation.Abstractions;
using KyrolusSous.Validation.Fluent;
using static KyrolusSous.Validation.FluentValidation.KyrolusValidationMessages;

namespace KyrolusSous.Validation.FluentValidation;

/// <summary>
/// Provides domain-specific validation extensions for FluentValidation (e.g. National IDs, Colors, URLs, Groups, Severities).
/// </summary>
public static class KyrolusFluentValidationExtensions
{
    /// <summary>Ensures the property is not empty and applies a standardized required error message.</summary>
    public static IRuleBuilderOptions<T, TProperty> Required<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "")
    {
        return ruleBuilder.NotEmpty()
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(IsRequired);
    }

    /// <summary>Ensures an audit integer field is greater than zero.</summary>
    public static IRuleBuilderOptions<T, int> ShouldCreatedBySomeone<T>(
        this IRuleBuilder<T, int> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "")
    {
        return ruleBuilder.GreaterThan(0)
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(ShouldBeCreatedBySomeone);
    }

    /// <summary>Ensures an identifier integer field is strictly positive (greater than 0).</summary>
    public static IRuleBuilderOptions<T, int> IdCanNotBeZero<T>(
        this IRuleBuilder<T, int> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "")
    {
        return ruleBuilder.GreaterThan(0)
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(CanNotBeZero);
    }

    /// <summary>Ensures a string property does not exceed the specified maximum length.</summary>
    public static IRuleBuilderOptions<T, string> HasMaximumLength<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        int length,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "")
    {
        return ruleBuilder.MaximumLength(length)
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(ExceedsMaxLength(length));
    }

    /// <summary>Validates that the string matches a hex color format (#RRGGBB).</summary>
    public static IRuleBuilderOptions<T, string> IsColor<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "")
    {
        return ruleBuilder
            .Matches(@"^#[0-9A-Fa-f]{6}$")
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(InvalidHexColor);
    }

    /// <summary>Validates that a collection or array property is not null or empty.</summary>
    public static IRuleBuilderOptions<T, TProperty> ArrayNotEmpty<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "")
    {
        return ruleBuilder.NotEmpty()
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(CanNotBeEmpty);
    }

    /// <summary>Validates that a string is a valid HTTP/HTTPS absolute URL.</summary>
    public static IRuleBuilderOptions<T, string> IsUrl<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "",
        bool isNullOrEmpty = false)
    {
        return ruleBuilder.Must(url =>
            {
                if (string.IsNullOrEmpty(url) && isNullOrEmpty)
                {
                    return true;
                }

                return !string.IsNullOrEmpty(url)
                    && Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            })
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(InvalidUrl);
    }

    /// <summary>Validates an Egyptian 14-digit National Identification Number format.</summary>
    public static IRuleBuilderOptions<T, string> IsEgyptianNationalId<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "",
        bool isNullOrEmpty = false)
    {
        return ruleBuilder.Must(id =>
            {
                if (string.IsNullOrEmpty(id) && isNullOrEmpty)
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(id) || id.Length != 14)
                {
                    return false;
                }

                return id[0] switch
                {
                    '2' or '3' => id.All(char.IsDigit),
                    _ => false
                };
            })
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(InvalidEgyptianNationalId);
    }

    /// <summary>Validates a Spanish DNI document number.</summary>
    public static IRuleBuilderOptions<T, string> IsSpanishDni<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "",
        bool isNullOrEmpty = false)
    {
        return ruleBuilder
            .Must(dni => (string.IsNullOrEmpty(dni) && isNullOrEmpty) || AdvancedRuleBuilderExtensions.IsSpanishDniValid(dni))
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(InvalidSpanishDni);
    }

    /// <summary>Validates a Spanish NIE document number.</summary>
    public static IRuleBuilderOptions<T, string> IsSpanishNie<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "",
        bool isNullOrEmpty = false)
    {
        return ruleBuilder
            .Must(nie => (string.IsNullOrEmpty(nie) && isNullOrEmpty) || AdvancedRuleBuilderExtensions.IsSpanishNieValid(nie))
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(InvalidSpanishNie);
    }

    /// <summary>Validates a Spanish CIF document number.</summary>
    public static IRuleBuilderOptions<T, string> IsSpanishCif<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "",
        bool isNullOrEmpty = false)
    {
        return ruleBuilder
            .Must(cif => (string.IsNullOrEmpty(cif) && isNullOrEmpty) || AdvancedRuleBuilderExtensions.IsSpanishCifValid(cif))
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(InvalidSpanishCif);
    }

    /// <summary>Validates a Spanish NIF document number.</summary>
    public static IRuleBuilderOptions<T, string> IsSpanishNif<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>>? expr = null,
        string propertyName = "",
        bool isNullOrEmpty = false)
    {
        return ruleBuilder
            .Must(nif => (string.IsNullOrEmpty(nif) && isNullOrEmpty) || AdvancedRuleBuilderExtensions.IsSpanishNifValid(nif))
            .ApplyPropertyName(expr, propertyName)
            .WithMessage(InvalidSpanishNif);
    }

    /// <summary>Associates a single logical group tag with this FluentValidation rule via CustomState.</summary>
    public static IRuleBuilderOptions<T, TProperty> WithGroup<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder, string groupName)
    {
        return ruleBuilder.WithState(_ => new KyrolusValidationGroup(groupName));
    }

    /// <summary>Associates a <see cref="KyrolusValidationGroup"/> instance with this FluentValidation rule via CustomState.</summary>
    public static IRuleBuilderOptions<T, TProperty> WithGroup<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder, KyrolusValidationGroup group)
    {
        return ruleBuilder.WithState(_ => group);
    }

    /// <summary>Associates multiple logical group tags with this FluentValidation rule via CustomState.</summary>
    public static IRuleBuilderOptions<T, TProperty> WithGroups<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder, params string[] groupNames)
    {
        return ruleBuilder.WithState(_ => new KyrolusValidationGroup(groupNames));
    }

    /// <summary>Associates a collection of logical group tags with this FluentValidation rule via CustomState.</summary>
    public static IRuleBuilderOptions<T, TProperty> WithGroups<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder, IEnumerable<string> groupNames)
    {
        return ruleBuilder.WithState(_ => new KyrolusValidationGroup(groupNames));
    }

    /// <summary>Maps and sets the severity level on the FluentValidation rule using <see cref="KyrolusValidationSeverity"/>.</summary>
    public static IRuleBuilderOptions<T, TProperty> WithSeverity<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder, KyrolusValidationSeverity severity)
    {
        var fvSeverity = severity switch
        {
            KyrolusValidationSeverity.Info => Severity.Info,
            KyrolusValidationSeverity.Warning => Severity.Warning,
            _ => Severity.Error
        };

        return ruleBuilder.WithSeverity(fvSeverity);
    }

    private static IRuleBuilderOptions<T, TProperty> ApplyPropertyName<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> builder,
        Expression<Func<T, object>>? expr,
        string propertyName)
    {
        var prop = ResolvePropertyName(expr, propertyName);
        return !string.IsNullOrWhiteSpace(prop) ? builder.OverridePropertyName(prop) : builder;
    }

    private static string ResolvePropertyName<T>(Expression<Func<T, object>>? expr, string propertyName)
    {
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            return propertyName;
        }

        if (expr is null)
        {
            return string.Empty;
        }

        return ReturnMemberExpression(expr);
    }

    private static string ReturnMemberExpression<T>(Expression<Func<T, object>> expr)
    {
        if (expr.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (expr.Body is UnaryExpression { Operand: MemberExpression operandMember })
        {
            return operandMember.Member.Name;
        }

        return string.Empty;
    }
}
