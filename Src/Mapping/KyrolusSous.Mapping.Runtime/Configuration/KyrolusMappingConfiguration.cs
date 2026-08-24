namespace KyrolusSous.Mapping.Runtime.Configuration;

/// <summary>
/// Central configuration container holding mapping profiles, custom rules, and type converters for <see cref="KyrolusObjectMapper"/>.
/// </summary>
public sealed class KyrolusMappingConfiguration
{
    private readonly ConcurrentDictionary<(Type Source, Type Target), KyrolusTypeMappingRule> _rules = new();
    private readonly ConcurrentDictionary<(Type Source, Type Target), object> _converters = new();
    private readonly List<KyrolusMappingProfile> _profiles = [];

    /// <summary>
    /// Gets or sets whether flattening (e.g. <c>Customer.Address.City</c> -> <c>CustomerAddressCity</c>) is enabled by default.
    /// </summary>
    public bool EnableFlattening { get; set; } = true;

    /// <summary>
    /// Gets or sets whether circular reference tracking is enabled by default.
    /// </summary>
    public bool EnableCircularReferenceTracking { get; set; } = true;

    /// <summary>
    /// Creates or retrieves a fluent mapping rule between <typeparamref name="TSource"/> and <typeparamref name="TTarget"/>.
    /// </summary>
    public KyrolusTypeMappingExpression<TSource, TTarget> CreateMap<TSource, TTarget>()
    {
        var rule = GetOrAddRule(typeof(TSource), typeof(TTarget));
        return new KyrolusTypeMappingExpression<TSource, TTarget>(rule, () => GetOrAddRule(typeof(TTarget), typeof(TSource)));
    }

    /// <summary>
    /// Gets or adds a rule for the specified source and target types.
    /// </summary>
    public KyrolusTypeMappingRule GetOrAddRule(Type sourceType, Type targetType)
    {
        var key = (sourceType, targetType);
        return _rules.GetOrAdd(key, k => new KyrolusTypeMappingRule(k.Source, k.Target));
    }

    /// <summary>
    /// Finds a registered custom rule for the specified source and target types.
    /// </summary>
    public KyrolusTypeMappingRule? FindRule(Type sourceType, Type targetType)
    {
        return _rules.TryGetValue((sourceType, targetType), out var rule) ? rule : null;
    }

    /// <summary>
    /// Adds and initializes a mapping profile instance.
    /// </summary>
    public KyrolusMappingConfiguration AddProfile(KyrolusMappingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Initialize(this);
        _profiles.Add(profile);
        return this;
    }

    /// <summary>
    /// Adds and initializes a mapping profile by type.
    /// </summary>
    public KyrolusMappingConfiguration AddProfile<TProfile>() where TProfile : KyrolusMappingProfile, new()
    {
        return AddProfile(new TProfile());
    }

    /// <summary>
    /// Registers a custom type converter instance.
    /// </summary>
    public KyrolusMappingConfiguration RegisterConverter<TSource, TTarget>(IKyrolusTypeConverter<TSource, TTarget> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters[(typeof(TSource), typeof(TTarget))] = converter;
        return this;
    }

    /// <summary>
    /// Retrieves a registered type converter for the specified type pair if available.
    /// </summary>
    public IKyrolusTypeConverter<TSource, TTarget>? FindConverter<TSource, TTarget>()
    {
        return _converters.TryGetValue((typeof(TSource), typeof(TTarget)), out var conv)
            ? conv as IKyrolusTypeConverter<TSource, TTarget>
            : null;
    }

    /// <summary>
    /// Validates all registered mapping rules, ensuring that all destination properties can be mapped, converted, or are explicitly ignored.
    /// Throws <see cref="KyrolusMappingValidationException"/> if any unmapped properties are found.
    /// </summary>
    public void AssertConfigurationIsValid()
    {
        var errors = new List<string>();

        foreach (var (key, rule) in _rules)
        {
            ValidateTypePairMapping(key.Source, key.Target, rule, EnableFlattening, errors);
        }

        if (errors.Count > 0)
        {
            throw new KyrolusMappingValidationException(
                $"KyrolusMapping configuration validation failed with {errors.Count} error(s):\n" +
                string.Join("\n", errors.Select((e, i) => $"{i + 1}. {e}")));
        }
    }

    private static void ValidateTypePairMapping(
        Type sourceType,
        Type targetType,
        KyrolusTypeMappingRule rule,
        bool enableFlattening,
        List<string> errors)
    {
        if (rule.CustomTypeConverter is not null)
        {
            return;
        }

        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        var unmapped = targetProps
            .Where(p => !IsTargetPropertyMapped(p, rule, sourceProps, sourceType, enableFlattening))
            .Select(p => p.Name)
            .ToList();

        if (unmapped.Count > 0)
        {
            errors.Add($"Mapping '{sourceType.Name}' -> '{targetType.Name}' has unmapped target properties: [{string.Join(", ", unmapped)}]. " +
                        $"Fix by adding .Ignore(dest => dest.{unmapped[0]}), [KyrolusIgnoreMap], or configuring .ForMember().");
        }
    }

    private static bool IsTargetPropertyMapped(
        PropertyInfo targetProp,
        KyrolusTypeMappingRule rule,
        Dictionary<string, PropertyInfo> sourceProps,
        Type sourceType,
        bool enableFlattening)
    {
        var propName = targetProp.Name;

        if (rule.IgnoredMembers.Contains(propName) ||
            targetProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null ||
            rule.CustomMemberResolvers.ContainsKey(propName))
        {
            return true;
        }

        var mapAttr = targetProp.GetCustomAttribute<KyrolusMapPropertyAttribute>();
        var sourceLookupName = mapAttr?.SourceName ?? (rule.PropertyNameMappings.TryGetValue(propName, out var alias) ? alias : propName);

        if (sourceProps.ContainsKey(sourceLookupName))
        {
            return true;
        }

        return enableFlattening && KyrolusMemberFlatteningResolver.ResolveFlattenedPath(sourceType, propName) is not null;
    }
}
