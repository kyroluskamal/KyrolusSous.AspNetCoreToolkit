namespace KyrolusSous.Mapping.Runtime.Configuration;

/// <summary>
/// Base class for modular, encapsulated mapping profiles in Clean Architecture and Enterprise solutions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Grouping related entity-to-DTO mappings into a dedicated, clean profile per feature area:
/// <code>
/// public class CatalogMappingProfile : KyrolusMappingProfile
/// {
///     public CatalogMappingProfile()
///     {
///         CreateMap&lt;Product, ProductResponseDto&gt;()
///             .ForMember(dest => dest.FormattedPrice, opt => opt.MapFrom(src => $"${src.Price:F2}"))
///             .ReverseMap();
///             
///         CreateMap&lt;Category, CategoryDto&gt;();
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class KyrolusMappingProfile
{
    private readonly Dictionary<(Type Source, Type Target), KyrolusTypeMappingRule> _rules = [];

    /// <summary>
    /// Attaches the mapping configuration container to this profile and applies all registered mappings.
    /// </summary>
    internal void Initialize(KyrolusMappingConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var (key, profileRule) in _rules)
        {
            var configRule = configuration.GetOrAddRule(key.Source, key.Target);
            profileRule.MergeInto(configRule);
        }
    }

    /// <summary>
    /// Creates a fluent mapping rule between <typeparamref name="TSource"/> and <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">The origin source type.</typeparam>
    /// <typeparam name="TTarget">The destination target type.</typeparam>
    /// <returns>A <see cref="KyrolusTypeMappingExpression{TSource, TTarget}"/> builder.</returns>
    protected KyrolusTypeMappingExpression<TSource, TTarget> CreateMap<TSource, TTarget>()
    {
        var rule = GetOrAddProfileRule(typeof(TSource), typeof(TTarget));
        return new KyrolusTypeMappingExpression<TSource, TTarget>(rule, () => GetOrAddProfileRule(typeof(TTarget), typeof(TSource)));
    }

    private KyrolusTypeMappingRule GetOrAddProfileRule(Type sourceType, Type targetType)
    {
        var key = (sourceType, targetType);
        if (!_rules.TryGetValue(key, out var rule))
        {
            rule = new KyrolusTypeMappingRule(sourceType, targetType);
            _rules[key] = rule;
        }

        return rule;
    }
}
