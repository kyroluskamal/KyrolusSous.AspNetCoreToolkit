namespace KyrolusSous.Mapping.Runtime.Configuration;

/// <summary>
/// Represents the compiled mapping metadata and customized member resolution rules between a source and target type pair.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KyrolusTypeMappingRule"/> class.
/// </remarks>
public sealed class KyrolusTypeMappingRule(Type sourceType, Type targetType)
{


    /// <summary>
    /// Gets the origin source type.
    /// </summary>
    public Type SourceType { get; } = sourceType ?? throw new ArgumentNullException(nameof(sourceType));

    /// <summary>
    /// Gets the destination target type.
    /// </summary>
    public Type TargetType { get; } = targetType ?? throw new ArgumentNullException(nameof(targetType));

    /// <summary>
    /// Gets the set of target property names that must be skipped during mapping.
    /// </summary>
    public HashSet<string> IgnoredMembers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets custom member resolution delegates mapped by destination property name.
    /// </summary>
    public Dictionary<string, Func<object, KyrolusMappingContext, object?>> CustomMemberResolvers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets conditional predicates per member determining whether a property should be copied.
    /// </summary>
    public Dictionary<string, Func<object, KyrolusMappingContext, bool>> MemberConditions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets property name overrides mapping target property names to specific source property paths.
    /// </summary>
    public Dictionary<string, string> PropertyNameMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets actions to execute before mapping properties.
    /// </summary>
    public List<Action<object, object, KyrolusMappingContext>> BeforeMapActions { get; } = [];

    /// <summary>
    /// Gets actions to execute after mapping properties.
    /// </summary>
    public List<Action<object, object, KyrolusMappingContext>> AfterMapActions { get; } = [];

    /// <summary>
    /// Gets or sets a whole-type custom converter delegate.
    /// </summary>
    public Func<object, KyrolusMappingContext, object>? CustomTypeConverter { get; set; }

    /// <summary>
    /// Gets or sets a custom instantiation factory delegate.
    /// </summary>
    public Delegate? CustomConstructor { get; set; }

    /// <summary>
    /// Gets or sets whether null source values should produce default/null targets rather than instantiating empty targets.
    /// </summary>
    public bool AllowNullDestinationValues { get; set; } = true;

    /// <summary>
    /// Gets or sets whether source properties with <c>null</c> values should be ignored during in-place mapping (HTTP PATCH behavior).
    /// </summary>
    public bool IgnoreNullValues { get; set; }

    /// <summary>
    /// Merges this mapping rule's settings and custom member resolvers into another target rule.
    /// </summary>
    /// <param name="target">The target rule to receive this rule's configuration.</param>
    public void MergeInto(KyrolusTypeMappingRule target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (var ignored in IgnoredMembers)
        {
            target.IgnoredMembers.Add(ignored);
        }

        foreach (var kvp in CustomMemberResolvers)
        {
            target.CustomMemberResolvers[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in MemberConditions)
        {
            target.MemberConditions[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in PropertyNameMappings)
        {
            target.PropertyNameMappings[kvp.Key] = kvp.Value;
        }

        foreach (var before in BeforeMapActions)
        {
            target.BeforeMapActions.Add(before);
        }

        foreach (var after in AfterMapActions)
        {
            target.AfterMapActions.Add(after);
        }

        if (CustomTypeConverter is not null)
        {
            target.CustomTypeConverter = CustomTypeConverter;
        }

        if (CustomConstructor is not null)
        {
            target.CustomConstructor = CustomConstructor;
        }

        if (IgnoreNullValues)
        {
            target.IgnoreNullValues = true;
        }

        target.AllowNullDestinationValues = AllowNullDestinationValues;
    }
}
