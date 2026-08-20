namespace KyrolusSous.Validation.FluentValidation;

public static class KyrolusFluentValidationExtensions
{
    public static IRuleBuilderOptions<T, TProperty> Required<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder, Expression<Func<T, object>> expr, string propertyName = "")
    {
        return ruleBuilder.NotEmpty()
                          .OverridePropertyName(ReturnMemberExpression(expr) ?? propertyName)
                          .WithMessage(IsRequired);
    }

    public static IRuleBuilderOptions<T, int> ShouldCreatedBySomeone<T>(this IRuleBuilder<T, int> ruleBuilder, Expression<Func<T, object>> expr, string propertyName = "")
    {
        return ruleBuilder.GreaterThan(0)
                          .OverridePropertyName(ReturnMemberExpression(expr) ?? propertyName)
                          .WithMessage(ShouldBeCreatedBySomeone);
    }

    public static IRuleBuilderOptions<T, int> IdCanNotBeZero<T>(this IRuleBuilder<T, int> ruleBuilder, Expression<Func<T, object>> expr, string propertyName = "")
    {
        return ruleBuilder.GreaterThan(0)
                          .OverridePropertyName(ReturnMemberExpression(expr) ?? propertyName)
                          .WithMessage(CanNotBeZero);
    }

    public static IRuleBuilderOptions<T, string> HasMaximumLength<T>(this IRuleBuilder<T, string> ruleBuilder, int length, Expression<Func<T, object>> expr, string propertyName = "")
    {
        return ruleBuilder.MaximumLength(length)
                          .OverridePropertyName(ReturnMemberExpression(expr) ?? propertyName)
                          .WithMessage(ExceedsMaxLength(length));
    }

    public static IRuleBuilderOptions<T, string> IsColor<T>(this IRuleBuilder<T, string> ruleBuilder, Expression<Func<T, object>> expr, string propertyName = "")
    {
        return ruleBuilder
            .Matches(@"^#[0-9A-Fa-f]{6}$")
            .OverridePropertyName(ReturnMemberExpression(expr) ?? propertyName)
            .WithMessage(InvalidHexColor);
    }

    public static IRuleBuilderOptions<T, TProperty> ArrayNotEmpty<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder, Expression<Func<T, object>> expr, string propertyName = "")
    {
        return ruleBuilder.NotEmpty()
                          .OverridePropertyName(ReturnMemberExpression(expr) ?? propertyName)
                          .WithMessage(CanNotBeEmpty);
    }

    public static IRuleBuilderOptions<T, string> IsUrl<T>(this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>> expr, string propertyName = "", bool isNullOrEmpty = false)
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
            .OverridePropertyName(string.IsNullOrEmpty(propertyName) ? ReturnMemberExpression(expr) : propertyName)
            .WithMessage(InvalidUrl);
    }

    public static IRuleBuilderOptions<T, string> IsEgyptianNationalId<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Expression<Func<T, object>> expr,
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
            .OverridePropertyName(string.IsNullOrEmpty(propertyName) ? ReturnMemberExpression(expr) : propertyName)
            .WithMessage(InvalidEgyptianNationalId);
    }

    public static IRuleBuilderOptions<T, TProperty> WithGroup<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder, string groupName)
    {
        return ruleBuilder.WithState(_ => new KyrolusValidationGroup(groupName));
    }

    public static IRuleBuilderOptions<T, TProperty> WithGroup<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder, KyrolusValidationGroup group)
    {
        return ruleBuilder.WithState(_ => group);
    }

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

    private static string ReturnMemberExpression<T>(Expression<Func<T, object>> expr)
    {
        MemberExpression? member = null;

        if (expr.Body is UnaryExpression unaryExpression)
        {
            member = unaryExpression.Operand as MemberExpression;
        }
        else if (expr.Body is MemberExpression memberExpression)
        {
            member = memberExpression;
        }

        if (member is null)
        {
            return string.Empty;
        }

        return member.Member.Name;
    }
}
