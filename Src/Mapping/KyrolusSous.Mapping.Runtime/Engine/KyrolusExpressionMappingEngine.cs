namespace KyrolusSous.Mapping.Runtime.Engine;

/// <summary>
/// Core dynamic mapping engine that analyzes type pairs and executes high-speed compiled mappings.
/// </summary>
public sealed class KyrolusExpressionMappingEngine
{
    private readonly KyrolusMappingConfiguration _configuration;
    private readonly ConcurrentDictionary<(Type Source, Type Target), Func<object, KyrolusMappingContext, IKyrolusObjectMapper, object>> _mappingCache = new();
    private readonly ConcurrentDictionary<(Type Source, Type Target), Action<object, object, KyrolusMappingContext, IKyrolusObjectMapper>> _inPlaceCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusExpressionMappingEngine"/> class.
    /// </summary>
    public KyrolusExpressionMappingEngine(KyrolusMappingConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Maps a source instance to a new target instance.
    /// </summary>
    public object? Map(Type sourceType, Type targetType, object? source, KyrolusMappingContext context, IKyrolusObjectMapper mapper)
    {
        if (source is null)
        {
            return null;
        }

        if (IsDirectlyAssignable(sourceType, targetType))
        {
            return source;
        }

        var rule = _configuration.FindRule(sourceType, targetType);
        if (rule?.CustomTypeConverter is not null)
        {
            return rule.CustomTypeConverter(source, context);
        }

        if (TryGetCircularReference(sourceType, targetType, source, context, out var existing))
        {
            return existing;
        }

        if (TryMapCollection(sourceType, targetType, source, context, mapper, out var collectionResult))
        {
            return collectionResult;
        }

        var mappingFunc = _mappingCache.GetOrAdd((sourceType, targetType), key => BuildMappingDelegate(key.Source, key.Target));
        return mappingFunc(source, context, mapper);
    }

    /// <summary>
    /// Maps properties from source onto an existing target instance.
    /// </summary>
    public void MapInPlace(Type sourceType, Type targetType, object source, object target, KyrolusMappingContext context, IKyrolusObjectMapper mapper)
    {
        if (source is null || target is null)
        {
            return;
        }

        var inPlaceAction = _inPlaceCache.GetOrAdd((sourceType, targetType), key => BuildInPlaceDelegate(key.Source, key.Target));
        inPlaceAction(source, target, context, mapper);
    }

    private static bool IsDirectlyAssignable(Type sourceType, Type targetType) =>
        (sourceType == typeof(string) || sourceType.IsPrimitive || sourceType.IsEnum || targetType == typeof(object)) &&
        targetType.IsAssignableFrom(sourceType);

    private bool TryGetCircularReference(Type sourceType, Type targetType, object source, KyrolusMappingContext context, out object? existing)
    {
        if (_configuration.EnableCircularReferenceTracking && !sourceType.IsValueType && !targetType.IsValueType)
        {
            return context.TryGetMapped(source, targetType, out existing);
        }

        existing = null;
        return false;
    }

    private bool TryMapCollection(Type sourceType, Type targetType, object source, KyrolusMappingContext context, IKyrolusObjectMapper mapper, out object? result)
    {
        if (KyrolusCollectionMappingHelper.IsCollectionType(sourceType, out var sourceElem) &&
            KyrolusCollectionMappingHelper.IsCollectionType(targetType, out var targetElem))
        {
            result = KyrolusCollectionMappingHelper.MapCollection(
                (IEnumerable)source,
                targetType,
                targetElem,
                (elem, ctx) => Map(elem?.GetType() ?? sourceElem, targetElem, elem, ctx, mapper),
                context);
            return true;
        }

        result = null;
        return false;
    }

    private Func<object, KyrolusMappingContext, IKyrolusObjectMapper, object> BuildMappingDelegate(Type sourceType, Type targetType)
    {
        var rule = _configuration.FindRule(sourceType, targetType);
        var ctor = ResolveConstructor(targetType);

        var sourceProps = GetReadableProperties(sourceType);
        var targetProps = GetWritableProperties(targetType);

        return (src, ctx, mapper) =>
        {
            var boundConstructorProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetInstance = CreateTargetInstance(sourceType, targetType, ctor, src, ctx, mapper, rule, sourceProps, boundConstructorProps);

            TrackCircularReference(sourceType, targetType, src, targetInstance, ctx);
            ExecuteHooks(rule?.BeforeMapActions, src, targetInstance, ctx);

            MapWritableProperties(sourceType, targetProps, boundConstructorProps, src, targetInstance, ctx, mapper, rule, sourceProps);
            ExecuteHooks(rule?.AfterMapActions, src, targetInstance, ctx);

            return targetInstance;
        };
    }

    private Action<object, object, KyrolusMappingContext, IKyrolusObjectMapper> BuildInPlaceDelegate(Type sourceType, Type targetType)
    {
        var rule = _configuration.FindRule(sourceType, targetType);
        var sourceProps = GetReadableProperties(sourceType);
        var targetProps = GetWritableProperties(targetType);

        return (src, targetInstance, ctx, mapper) =>
        {
            TrackCircularReference(sourceType, targetType, src, targetInstance, ctx);
            ExecuteHooks(rule?.BeforeMapActions, src, targetInstance, ctx);

            MapWritablePropertiesInPlace(sourceType, targetProps, src, targetInstance, ctx, mapper, rule, sourceProps);
            ExecuteHooks(rule?.AfterMapActions, src, targetInstance, ctx);
        };
    }

    private static ConstructorInfo? ResolveConstructor(Type targetType)
    {
        var constructors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var explicitCtor = constructors.FirstOrDefault(c => c.GetCustomAttribute<KyrolusMapConstructorAttribute>() is not null);
        var parameterlessCtor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        return explicitCtor ?? parameterlessCtor ?? constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
    }

    private static Dictionary<string, PropertyInfo> GetReadableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

    private static List<PropertyInfo> GetWritableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

    private object CreateTargetInstance(
        Type sourceType,
        Type targetType,
        ConstructorInfo? ctor,
        object src,
        KyrolusMappingContext ctx,
        IKyrolusObjectMapper mapper,
        KyrolusTypeMappingRule? rule,
        Dictionary<string, PropertyInfo> sourceProps,
        HashSet<string> boundConstructorProps)
    {
        if (rule?.CustomConstructor is not null)
        {
            return rule.CustomConstructor.DynamicInvoke(src)!;
        }

        if (ctor is not null && ctor.GetParameters().Length > 0)
        {
            var args = BuildConstructorArgs(sourceType, ctor, src, ctx, mapper, rule, sourceProps, boundConstructorProps);
            return ctor.Invoke(args);
        }

        return Activator.CreateInstance(targetType)!;
    }

    private object?[] BuildConstructorArgs(
        Type sourceType,
        ConstructorInfo ctor,
        object src,
        KyrolusMappingContext ctx,
        IKyrolusObjectMapper mapper,
        KyrolusTypeMappingRule? rule,
        Dictionary<string, PropertyInfo> sourceProps,
        HashSet<string> boundConstructorProps)
    {
        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name ?? string.Empty;
            boundConstructorProps.Add(paramName);

            args[i] = ResolveConstructorParameterValue(sourceType, param, paramName, src, ctx, mapper, rule, sourceProps);
        }

        return args;
    }

    private object? ResolveConstructorParameterValue(
        Type sourceType,
        ParameterInfo param,
        string paramName,
        object src,
        KyrolusMappingContext ctx,
        IKyrolusObjectMapper mapper,
        KyrolusTypeMappingRule? rule,
        Dictionary<string, PropertyInfo> sourceProps)
    {
        if (rule?.CustomMemberResolvers.TryGetValue(paramName, out var customResolver) == true)
        {
            return customResolver(src, ctx);
        }

        if (sourceProps.TryGetValue(paramName, out var matchedSourceProp))
        {
            var rawVal = matchedSourceProp.GetValue(src);
            return MapValue(rawVal, matchedSourceProp.PropertyType, param.ParameterType, ctx, mapper);
        }

        if (_configuration.EnableFlattening &&
            KyrolusMemberFlatteningResolver.ResolveFlattenedPath(sourceType, paramName) is { } path)
        {
            var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
            return MapValue(rawVal, path.Last().PropertyType, param.ParameterType, ctx, mapper);
        }

        return param.HasDefaultValue
            ? param.DefaultValue
            : (param.ParameterType.IsValueType ? Activator.CreateInstance(param.ParameterType) : null);
    }

    private void MapWritableProperties(
        Type sourceType,
        List<PropertyInfo> targetProps,
        HashSet<string> boundConstructorProps,
        object src,
        object targetInstance,
        KyrolusMappingContext ctx,
        IKyrolusObjectMapper mapper,
        KyrolusTypeMappingRule? rule,
        Dictionary<string, PropertyInfo> sourceProps)
    {
        foreach (var targetProp in targetProps)
        {
            if (boundConstructorProps.Contains(targetProp.Name))
            {
                continue;
            }

            MapSingleProperty(sourceType, targetProp, src, targetInstance, ctx, mapper, rule, sourceProps);
        }
    }

    private void MapSingleProperty(
        Type sourceType,
        PropertyInfo targetProp,
        object src,
        object targetInstance,
        KyrolusMappingContext ctx,
        IKyrolusObjectMapper mapper,
        KyrolusTypeMappingRule? rule,
        Dictionary<string, PropertyInfo> sourceProps)
    {
        var propName = targetProp.Name;

        if (IsPropertyIgnored(targetProp, propName, rule))
        {
            return;
        }

        if (rule?.MemberConditions.TryGetValue(propName, out var condition) == true && !condition(src, ctx))
        {
            return;
        }

        if (rule?.CustomMemberResolvers.TryGetValue(propName, out var customResolver) == true)
        {
            targetProp.SetValue(targetInstance, customResolver(src, ctx));
            return;
        }

        var sourceLookupName = ResolveSourcePropertyName(targetProp, propName, rule);

        if (sourceProps.TryGetValue(sourceLookupName, out var sourceProp))
        {
            if (sourceProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
            {
                return;
            }

            var rawVal = sourceProp.GetValue(src);
            var mappedVal = MapValue(rawVal, sourceProp.PropertyType, targetProp.PropertyType, ctx, mapper);
            targetProp.SetValue(targetInstance, mappedVal);
        }
        else if (_configuration.EnableFlattening &&
                 KyrolusMemberFlatteningResolver.ResolveFlattenedPath(sourceType, propName) is { } path)
        {
            var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
            var mappedVal = MapValue(rawVal, path.Last().PropertyType, targetProp.PropertyType, ctx, mapper);
            targetProp.SetValue(targetInstance, mappedVal);
        }
    }

    private void MapWritablePropertiesInPlace(
        Type sourceType,
        List<PropertyInfo> targetProps,
        object src,
        object targetInstance,
        KyrolusMappingContext ctx,
        IKyrolusObjectMapper mapper,
        KyrolusTypeMappingRule? rule,
        Dictionary<string, PropertyInfo> sourceProps)
    {
        foreach (var targetProp in targetProps)
        {
            MapSinglePropertyInPlace(sourceType, targetProp, src, targetInstance, ctx, mapper, rule, sourceProps);
        }
    }

    private void MapSinglePropertyInPlace(
        Type sourceType,
        PropertyInfo targetProp,
        object src,
        object targetInstance,
        KyrolusMappingContext ctx,
        IKyrolusObjectMapper mapper,
        KyrolusTypeMappingRule? rule,
        Dictionary<string, PropertyInfo> sourceProps)
    {
        var propName = targetProp.Name;

        if (IsPropertyIgnored(targetProp, propName, rule))
        {
            return;
        }

        if (rule?.MemberConditions.TryGetValue(propName, out var condition) == true && !condition(src, ctx))
        {
            return;
        }

        if (rule?.CustomMemberResolvers.TryGetValue(propName, out var customResolver) == true)
        {
            targetProp.SetValue(targetInstance, customResolver(src, ctx));
            return;
        }

        var sourceLookupName = ResolveSourcePropertyName(targetProp, propName, rule);

        if (sourceProps.TryGetValue(sourceLookupName, out var sourceProp))
        {
            if (sourceProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
            {
                return;
            }

            var rawVal = sourceProp.GetValue(src);
            if (ShouldIgnoreNull(sourceType, sourceProp, targetProp, rule, rawVal))
            {
                return;
            }

            var mappedVal = MapValue(rawVal, sourceProp.PropertyType, targetProp.PropertyType, ctx, mapper);
            targetProp.SetValue(targetInstance, mappedVal);
        }
        else if (_configuration.EnableFlattening &&
                 KyrolusMemberFlatteningResolver.ResolveFlattenedPath(sourceType, propName) is { } path)
        {
            var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
            if (ShouldIgnoreNull(sourceType, null, targetProp, rule, rawVal))
            {
                return;
            }

            var mappedVal = MapValue(rawVal, path.Last().PropertyType, targetProp.PropertyType, ctx, mapper);
            targetProp.SetValue(targetInstance, mappedVal);
        }
    }

    private static bool IsPropertyIgnored(PropertyInfo targetProp, string propName, KyrolusTypeMappingRule? rule) =>
        (rule?.IgnoredMembers.Contains(propName) == true) ||
        targetProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null;

    private static string ResolveSourcePropertyName(PropertyInfo targetProp, string propName, KyrolusTypeMappingRule? rule)
    {
        var mapAttr = targetProp.GetCustomAttribute<KyrolusMapPropertyAttribute>();
        return mapAttr?.SourceName ?? (rule?.PropertyNameMappings.TryGetValue(propName, out var alias) == true ? alias : propName);
    }

    private static bool ShouldIgnoreNull(
        Type sourceType,
        PropertyInfo? sourceProp,
        PropertyInfo targetProp,
        KyrolusTypeMappingRule? rule,
        object? rawVal)
    {
        if (rawVal is not null)
        {
            return false;
        }

        return (rule?.IgnoreNullValues == true) ||
               sourceType.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null ||
               (sourceProp is not null && sourceProp.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null) ||
               targetProp.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null;
    }

    private void TrackCircularReference(Type sourceType, Type targetType, object src, object targetInstance, KyrolusMappingContext ctx)
    {
        if (_configuration.EnableCircularReferenceTracking && !sourceType.IsValueType && !targetType.IsValueType)
        {
            ctx.RegisterMapped(src, targetInstance);
        }
    }

    private static void ExecuteHooks(IReadOnlyList<Action<object, object, KyrolusMappingContext>>? hooks, object src, object targetInstance, KyrolusMappingContext ctx)
    {
        if (hooks is { Count: > 0 })
        {
            foreach (var hook in hooks)
            {
                hook(src, targetInstance, ctx);
            }
        }
    }

    private object? MapValue(object? value, Type sourceType, Type targetType, KyrolusMappingContext context, IKyrolusObjectMapper mapper)
    {
        if (value is null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        if (targetType.IsAssignableFrom(sourceType))
        {
            return value;
        }

        var underlyingTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var underlyingSource = Nullable.GetUnderlyingType(sourceType) ?? sourceType;

        if (underlyingTarget.IsAssignableFrom(underlyingSource))
        {
            return value;
        }

        if (targetType == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        if (TryConvertEnum(value, underlyingSource, underlyingTarget, out var enumResult))
        {
            return enumResult;
        }

        if (TryConvertGuid(value, underlyingSource, underlyingTarget, out var guidResult))
        {
            return guidResult;
        }

        if (TryConvertDateOnly(value, underlyingSource, underlyingTarget, out var dateResult))
        {
            return dateResult;
        }

        if (TryConvertTimeOnly(value, underlyingSource, underlyingTarget, out var timeResult))
        {
            return timeResult;
        }

        if (TryConvertDateTime(value, underlyingTarget, out var dtResult))
        {
            return dtResult;
        }

        if (TryConvertPrimitive(value, underlyingSource, underlyingTarget, out var primResult))
        {
            return primResult;
        }

        return Map(underlyingSource, underlyingTarget, value, context, mapper);
    }

    private static bool TryConvertEnum(object value, Type source, Type target, out object? result)
    {
        if (target.IsEnum)
        {
            result = value is string str
                ? Enum.Parse(target, str, ignoreCase: true)
                : Enum.ToObject(target, value);
            return true;
        }

        if (source.IsEnum && target == typeof(string))
        {
            result = value.ToString();
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertGuid(object value, Type source, Type target, out object? result)
    {
        if (target == typeof(Guid) && value is string guidStr)
        {
            result = Guid.TryParse(guidStr, out var parsedGuid) ? parsedGuid : Guid.Empty;
            return true;
        }

        if (source == typeof(Guid) && target == typeof(string))
        {
            result = value.ToString();
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertDateOnly(object value, Type source, Type target, out object? result)
    {
        if (target == typeof(DateOnly))
        {
            if (value is DateTime dt)
            {
                result = DateOnly.FromDateTime(dt);
                return true;
            }

            if (value is string str && DateOnly.TryParse(str, CultureInfo.InvariantCulture, out var parsed))
            {
                result = parsed;
                return true;
            }
        }

        if (source == typeof(DateOnly))
        {
            if (target == typeof(DateTime) && value is DateOnly d)
            {
                result = d.ToDateTime(TimeOnly.MinValue);
                return true;
            }

            if (target == typeof(string) && value is DateOnly dStr)
            {
                result = dStr.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;
            }
        }

        result = null;
        return false;
    }

    private static bool TryConvertTimeOnly(object value, Type source, Type target, out object? result)
    {
        if (target == typeof(TimeOnly))
        {
            if (value is TimeSpan ts)
            {
                result = TimeOnly.FromTimeSpan(ts);
                return true;
            }

            if (value is DateTime dt)
            {
                result = TimeOnly.FromDateTime(dt);
                return true;
            }

            if (value is string str && TimeOnly.TryParse(str, CultureInfo.InvariantCulture, out var parsed))
            {
                result = parsed;
                return true;
            }
        }

        if (source == typeof(TimeOnly))
        {
            if (target == typeof(TimeSpan) && value is TimeOnly t)
            {
                result = t.ToTimeSpan();
                return true;
            }

            if (target == typeof(string) && value is TimeOnly tStr)
            {
                result = tStr.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                return true;
            }
        }

        result = null;
        return false;
    }

    private static bool TryConvertDateTime(object value, Type target, out object? result)
    {
        if (target == typeof(DateTimeOffset) && value is DateTime dt)
        {
            if (dt == DateTime.MinValue)
            {
                result = DateTimeOffset.MinValue;
                return true;
            }

            if (dt == DateTime.MaxValue)
            {
                result = DateTimeOffset.MaxValue;
                return true;
            }

            result = dt.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(dt, TimeSpan.Zero)
                : new DateTimeOffset(dt);
            return true;
        }

        if (target == typeof(DateTime) && value is DateTimeOffset dto)
        {
            result = dto.DateTime;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertPrimitive(object value, Type source, Type target, out object? result)
    {
        if (typeof(IConvertible).IsAssignableFrom(source) && typeof(IConvertible).IsAssignableFrom(target))
        {
            try
            {
                result = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                // Fallback to recursive mapping
            }
        }

        result = null;
        return false;
    }
}
