namespace KyrolusSous.Mapping.Runtime.Configuration;

/// <summary>
/// Fluent configuration builder for custom mapping definitions between <typeparamref name="TSource"/> and <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">The origin source type.</typeparam>
/// <typeparam name="TTarget">The destination target type.</typeparam>
public sealed class KyrolusTypeMappingExpression<TSource, TTarget>
{
    private readonly KyrolusTypeMappingRule _rule;
    private readonly Func<KyrolusTypeMappingRule>? _reverseRuleFactory;

    internal KyrolusTypeMappingExpression(KyrolusTypeMappingRule rule, Func<KyrolusTypeMappingRule>? reverseRuleFactory = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _reverseRuleFactory = reverseRuleFactory;
    }

    /// <summary>
    /// Configures a specific destination property member.
    /// </summary>
    /// <typeparam name="TMember">The destination property type.</typeparam>
    /// <param name="destinationMember">Expression targeting the destination property.</param>
    /// <param name="memberOptions">Action configuring the member resolution options.</param>
    /// <returns>The current configuration builder for method chaining.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> ForMember<TMember>(
        Expression<Func<TTarget, TMember>> destinationMember,
        Action<KyrolusMemberConfigurationExpression<TSource, TTarget, TMember>> memberOptions)
    {
        ArgumentNullException.ThrowIfNull(destinationMember);
        ArgumentNullException.ThrowIfNull(memberOptions);

        var memberName = GetMemberName(destinationMember);
        var expr = new KyrolusMemberConfigurationExpression<TSource, TTarget, TMember>(memberName, _rule);
        memberOptions(expr);
        return this;
    }

    /// <summary>
    /// Excludes a specific destination property from being mapped.
    /// </summary>
    /// <typeparam name="TMember">The destination property type.</typeparam>
    /// <param name="destinationMember">Expression targeting the destination property to ignore.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> Ignore<TMember>(Expression<Func<TTarget, TMember>> destinationMember)
    {
        ArgumentNullException.ThrowIfNull(destinationMember);
        var memberName = GetMemberName(destinationMember);
        _rule.IgnoredMembers.Add(memberName);
        return this;
    }

    /// <summary>
    /// Configures an action to be executed before mapping properties.
    /// </summary>
    /// <param name="action">Action invoked with source and target instances.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> BeforeMap(Action<TSource, TTarget> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _rule.BeforeMapActions.Add((src, dest, _) => action((TSource)src, (TTarget)dest));
        return this;
    }

    /// <summary>
    /// Configures an action to be executed before mapping properties with access to <see cref="KyrolusMappingContext"/>.
    /// </summary>
    /// <param name="action">Action invoked with source, target, and context.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> BeforeMap(Action<TSource, TTarget, KyrolusMappingContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _rule.BeforeMapActions.Add((src, dest, ctx) => action((TSource)src, (TTarget)dest, ctx));
        return this;
    }

    /// <summary>
    /// Configures an action to be executed after mapping properties.
    /// </summary>
    /// <param name="action">Action invoked with source and target instances.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> AfterMap(Action<TSource, TTarget> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _rule.AfterMapActions.Add((src, dest, _) => action((TSource)src, (TTarget)dest));
        return this;
    }

    /// <summary>
    /// Configures an action to be executed after mapping properties with access to <see cref="KyrolusMappingContext"/>.
    /// </summary>
    /// <param name="action">Action invoked with source, target, and context.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> AfterMap(Action<TSource, TTarget, KyrolusMappingContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _rule.AfterMapActions.Add((src, dest, ctx) => action((TSource)src, (TTarget)dest, ctx));
        return this;
    }

    /// <summary>
    /// Configures an entire custom type conversion function.
    /// </summary>
    /// <param name="converter">Custom conversion delegate.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> ConvertUsing(Func<TSource, TTarget> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _rule.CustomTypeConverter = (src, _) => converter((TSource)src)!;
        return this;
    }

    /// <summary>
    /// Configures a custom type converter with access to <see cref="KyrolusMappingContext"/>.
    /// </summary>
    /// <param name="converter">Custom conversion delegate with context.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> ConvertUsing(Func<TSource, KyrolusMappingContext, TTarget> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _rule.CustomTypeConverter = (src, ctx) => converter((TSource)src, ctx)!;
        return this;
    }

    /// <summary>
    /// Configures the mapping to ignore <c>null</c> source values during in-place mutation (HTTP PATCH semantics).
    /// </summary>
    /// <param name="ignore">Whether to ignore null source values.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> IgnoreNullValues(bool ignore = true)
    {
        _rule.IgnoreNullValues = ignore;
        return this;
    }

    /// <summary>
    /// Configures custom construction logic for the target type.
    /// </summary>
    /// <param name="constructor">Factory function to instantiate the target type.</param>
    /// <returns>The current configuration builder.</returns>
    public KyrolusTypeMappingExpression<TSource, TTarget> ConstructUsing(Func<TSource, TTarget> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        _rule.CustomConstructor = constructor;
        return this;
    }

    /// <summary>
    /// Automatically creates and registers an inverted reverse mapping rule from <typeparamref name="TTarget"/> to <typeparamref name="TSource"/>.
    /// </summary>
    /// <returns>A new <see cref="KyrolusTypeMappingExpression{TTarget, TSource}"/> for configuring the reverse direction.</returns>
    public KyrolusTypeMappingExpression<TTarget, TSource> ReverseMap()
    {
        if (_reverseRuleFactory is null)
        {
            throw new InvalidOperationException("Reverse mapping is not supported when no reverse rule factory is provided.");
        }

        var reverseRule = _reverseRuleFactory();
        return new KyrolusTypeMappingExpression<TTarget, TSource>(reverseRule, () => _rule);
    }

    private static string GetMemberName<TMember>(Expression<Func<TTarget, TMember>> expression)
    {
        if (expression.Body is MemberExpression memberExpr)
        {
            return memberExpr.Member.Name;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression innerMember })
        {
            return innerMember.Member.Name;
        }

        throw new ArgumentException($"Expression '{expression}' does not refer to a property or field.", nameof(expression));
    }
}

/// <summary>
/// Fluent configuration options for a single destination property.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
/// <typeparam name="TMember">The destination property type.</typeparam>
public sealed class KyrolusMemberConfigurationExpression<TSource, TTarget, TMember>
{
    private readonly string _memberName;
    private readonly KyrolusTypeMappingRule _rule;

    internal KyrolusMemberConfigurationExpression(string memberName, KyrolusTypeMappingRule rule)
    {
        _memberName = memberName;
        _rule = rule;
    }

    /// <summary>
    /// Maps the destination member from a custom source property expression.
    /// </summary>
    /// <param name="sourceExpression">Expression extracting or computing the member value from source.</param>
    public void MapFrom<TSourceMember>(Func<TSource, TSourceMember> sourceExpression)
    {
        ArgumentNullException.ThrowIfNull(sourceExpression);
        _rule.CustomMemberResolvers[_memberName] = (src, _) => sourceExpression((TSource)src);
    }

    /// <summary>
    /// Maps the destination member using a resolver with access to <see cref="KyrolusMappingContext"/>.
    /// </summary>
    /// <param name="resolver">Function resolving the member value from source and context.</param>
    public void MapFrom<TSourceMember>(Func<TSource, KyrolusMappingContext, TSourceMember> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _rule.CustomMemberResolvers[_memberName] = (src, ctx) => resolver((TSource)src, ctx);
    }

    /// <summary>
    /// Sets a condition that must evaluate to <c>true</c> for this member to be mapped.
    /// </summary>
    /// <param name="predicate">Predicate function receiving the source object.</param>
    public void Condition(Func<TSource, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _rule.MemberConditions[_memberName] = (src, _) => predicate((TSource)src);
    }

    /// <summary>
    /// Sets a condition with context that must evaluate to <c>true</c> for this member to be mapped.
    /// </summary>
    /// <param name="predicate">Predicate function receiving the source object and context.</param>
    public void Condition(Func<TSource, KyrolusMappingContext, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _rule.MemberConditions[_memberName] = (src, ctx) => predicate((TSource)src, ctx);
    }

    /// <summary>
    /// Assigns a constant value to the destination member.
    /// </summary>
    /// <param name="value">The constant value to set.</param>
    public void UseValue(TMember value)
    {
        _rule.CustomMemberResolvers[_memberName] = (_, _) => value;
    }

    /// <summary>
    /// Excludes this member from mapping.
    /// </summary>
    public void Ignore()
    {
        _rule.IgnoredMembers.Add(_memberName);
    }
}
