namespace KyrolusSous.ValidationBase.ValidationExtensions;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, TProperty> Required<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder, Expression<Func<T, object>> expr, string PropertyName = "")
    {
        return ruleBuilder.NotEmpty()
                          .OverridePropertyName(ReturnMemberExpresion(expr) ?? PropertyName)
                          .WithMessage(IsRequired);
    }
    public static IRuleBuilderOptions<T, int> ShouldCreatedBySomeone<T>(this IRuleBuilder<T, int> ruleBuilder, Expression<Func<T, object>> expr, string PropertyName = "")
    {
        return ruleBuilder.GreaterThan(0)
                          .OverridePropertyName(ReturnMemberExpresion(expr) ?? PropertyName)
                          .WithMessage(ShouldBeCreatedBySomeone);
    }

    public static IRuleBuilderOptions<T, int> IdCanNotBeZero<T>(this IRuleBuilder<T, int> ruleBuilder, Expression<Func<T, object>> expr, string PropertyName = "")
    {
        return ruleBuilder.GreaterThan(0).OverridePropertyName(ReturnMemberExpresion(expr) ?? PropertyName)
        .WithMessage(CanNotBeZero);
    }

    public static IRuleBuilderOptions<T, string> HasMaximumLength<T>(this IRuleBuilder<T, string> ruleBuilder, int length, Expression<Func<T, object>> expr, string PropertyName = "")
    {
        return ruleBuilder.MaximumLength(length)
                          .OverridePropertyName(ReturnMemberExpresion(expr) ?? PropertyName)
                          .WithMessage(ExceedsMaxLength(length));
    }
    public static IRuleBuilderOptions<T, string> IsColor<T>(this IRuleBuilder<T, string> ruleBuilder, Expression<Func<T, object>> expr, string PropertyName = "")
    {
        return ruleBuilder
            .Matches(@"^#[0-9A-Fa-f]{6}$")
                                      .OverridePropertyName(ReturnMemberExpresion(expr) ?? PropertyName)

            .WithMessage(InvalidHexColor);
    }
    public static IRuleBuilderOptions<T, TProperty> ArrayNotEmpty<T, TProperty>(
            this IRuleBuilder<T, TProperty> ruleBuilder, Expression<Func<T, object>> expr, string PropertyName = "")
    {
        return ruleBuilder.NotEmpty()
                          .OverridePropertyName(ReturnMemberExpresion(expr) ?? PropertyName)
                          .WithMessage(CanNotBeEmpty);
    }
    public static IRuleBuilderOptions<T, string> IsUrl<T>(this IRuleBuilder<T, string> ruleBuilder,
    Expression<Func<T, object>> expr, string PropertyName = "", bool IsNullOrEmpty = false)
    {

        return ruleBuilder.Must(url =>
        {
            if (url == null && IsNullOrEmpty)
                return true;

            return !string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        })
        .OverridePropertyName(string.IsNullOrEmpty(PropertyName) ? ReturnMemberExpresion(expr) : PropertyName)
        .WithMessage(InvalidUrl);
    }
    private static string ReturnMemberExpresion<T>(Expression<Func<T, object>> expr)
    {
        MemberExpression? member = null;

        // Check if it's a UnaryExpression (e.g., boxing of a value type to object)
        if (expr.Body is UnaryExpression unaryExpression)
        {
            // Get the operand (actual expression)
            member = unaryExpression.Operand as MemberExpression;
        }
        else if (expr.Body is MemberExpression memberExpression)
        {
            // It's already a MemberExpression, so we can directly use it
            member = memberExpression;
        }

        if (member == null)
            return string.Empty;
        // throw new NotSupportedException("Must supply a MemberExpression when calling OverridePropertyName");

        return member.Member.Name;
    }

}
