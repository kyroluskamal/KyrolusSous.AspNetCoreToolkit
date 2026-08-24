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

        // Direct assignability check for immutable primitive types / strings
        if (sourceType == typeof(string) || sourceType.IsPrimitive || sourceType.IsEnum || targetType == typeof(object))
        {
            if (targetType.IsAssignableFrom(sourceType))
            {
                return source;
            }
        }

        // Check if a whole-type custom converter or rule is registered
        var rule = _configuration.FindRule(sourceType, targetType);
        if (rule?.CustomTypeConverter is not null)
        {
            return rule.CustomTypeConverter(source, context);
        }

        // Check circular reference cache if tracking is enabled
        if (_configuration.EnableCircularReferenceTracking && !sourceType.IsValueType && !targetType.IsValueType)
        {
            if (context.TryGetMapped(source, targetType, out var existing))
            {
                return existing;
            }
        }

        // Check collection types
        if (KyrolusCollectionMappingHelper.IsCollectionType(sourceType, out var sourceElem) &&
            KyrolusCollectionMappingHelper.IsCollectionType(targetType, out var targetElem))
        {
            return KyrolusCollectionMappingHelper.MapCollection(
                (IEnumerable)source,
                targetType,
                sourceElem,
                targetElem,
                (elem, ctx) => Map(elem?.GetType() ?? sourceElem, targetElem, elem, ctx, mapper),
                context);
        }

        // Execute or compile mapping delegate
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

    private Func<object, KyrolusMappingContext, IKyrolusObjectMapper, object> BuildMappingDelegate(Type sourceType, Type targetType)
    {
        var rule = _configuration.FindRule(sourceType, targetType);

        // Check for constructor binding (prefer parameterless constructor unless [KyrolusMapConstructor] is present or no parameterless ctor exists)
        var constructors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var explicitCtor = constructors.FirstOrDefault(c => c.GetCustomAttribute<KyrolusMapConstructorAttribute>() is not null);
        var parameterlessCtor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        var ctor = explicitCtor ?? parameterlessCtor ?? constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        return (src, ctx, mapper) =>
        {
            object targetInstance;
            var boundConstructorProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Construct target
            if (rule?.CustomConstructor is not null)
            {
                targetInstance = rule.CustomConstructor.DynamicInvoke(src)!;
            }
            else if (ctor is not null && ctor.GetParameters().Length > 0)
            {
                var parameters = ctor.GetParameters();
                var args = new object?[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramName = param.Name ?? string.Empty;
                    boundConstructorProps.Add(paramName);

                    if (rule?.CustomMemberResolvers.TryGetValue(paramName, out var customResolver) == true)
                    {
                        args[i] = customResolver(src, ctx);
                    }
                    else if (sourceProps.TryGetValue(paramName, out var matchedSourceProp))
                    {
                        var rawVal = matchedSourceProp.GetValue(src);
                        args[i] = MapValue(rawVal, matchedSourceProp.PropertyType, param.ParameterType, ctx, mapper);
                    }
                    else if (_configuration.EnableFlattening &&
                             KyrolusMemberFlatteningResolver.ResolveFlattenedPath(sourceType, paramName) is { } path)
                    {
                        var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
                        args[i] = MapValue(rawVal, path.Last().PropertyType, param.ParameterType, ctx, mapper);
                    }
                    else
                    {
                        args[i] = param.HasDefaultValue ? param.DefaultValue : (param.ParameterType.IsValueType ? Activator.CreateInstance(param.ParameterType) : null);
                    }
                }

                targetInstance = ctor.Invoke(args);
            }
            else
            {
                targetInstance = Activator.CreateInstance(targetType)!;
            }

            // Register in circular reference tracker
            if (_configuration.EnableCircularReferenceTracking && !sourceType.IsValueType && !targetType.IsValueType)
            {
                ctx.RegisterMapped(src, targetInstance);
            }

            // BeforeMap hooks
            if (rule?.BeforeMapActions.Count > 0)
            {
                foreach (var before in rule.BeforeMapActions)
                {
                    before(src, targetInstance, ctx);
                }
            }

            // 2. Set writable properties
            foreach (var targetProp in targetProps)
            {
                var propName = targetProp.Name;

                // If property was already set via constructor parameter, don't overwrite
                if (boundConstructorProps.Contains(propName))
                {
                    continue;
                }

                // Check ignore list
                if (rule?.IgnoredMembers.Contains(propName) == true ||
                    targetProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
                {
                    continue;
                }

                // Check member condition predicate
                if (rule?.MemberConditions.TryGetValue(propName, out var condition) == true && !condition(src, ctx))
                {
                    continue;
                }

                // Check custom member resolvers
                if (rule?.CustomMemberResolvers.TryGetValue(propName, out var customResolver) == true)
                {
                    var resolved = customResolver(src, ctx);
                    targetProp.SetValue(targetInstance, resolved);
                    continue;
                }

                // Check MapProperty attribute on target
                var mapAttr = targetProp.GetCustomAttribute<KyrolusMapPropertyAttribute>();
                var sourceLookupName = mapAttr?.SourceName ?? (rule?.PropertyNameMappings.TryGetValue(propName, out var alias) == true ? alias : propName);

                if (sourceProps.TryGetValue(sourceLookupName, out var sourceProp))
                {
                    if (sourceProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
                    {
                        continue;
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

            // AfterMap hooks
            if (rule?.AfterMapActions.Count > 0)
            {
                foreach (var after in rule.AfterMapActions)
                {
                    after(src, targetInstance, ctx);
                }
            }

            return targetInstance;
        };
    }

    private Action<object, object, KyrolusMappingContext, IKyrolusObjectMapper> BuildInPlaceDelegate(Type sourceType, Type targetType)
    {
        var rule = _configuration.FindRule(sourceType, targetType);

        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var targetProps = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        return (src, targetInstance, ctx, mapper) =>
        {
            if (_configuration.EnableCircularReferenceTracking && !sourceType.IsValueType && !targetType.IsValueType)
            {
                ctx.RegisterMapped(src, targetInstance);
            }

            // BeforeMap hooks
            if (rule?.BeforeMapActions.Count > 0)
            {
                foreach (var before in rule.BeforeMapActions)
                {
                    before(src, targetInstance, ctx);
                }
            }

            foreach (var targetProp in targetProps)
            {
                var propName = targetProp.Name;

                if (rule?.IgnoredMembers.Contains(propName) == true ||
                    targetProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
                {
                    continue;
                }

                // Check member condition predicate
                if (rule?.MemberConditions.TryGetValue(propName, out var condition) == true && !condition(src, ctx))
                {
                    continue;
                }

                if (rule?.CustomMemberResolvers.TryGetValue(propName, out var customResolver) == true)
                {
                    var resolved = customResolver(src, ctx);
                    targetProp.SetValue(targetInstance, resolved);
                    continue;
                }

                var mapAttr = targetProp.GetCustomAttribute<KyrolusMapPropertyAttribute>();
                var sourceLookupName = mapAttr?.SourceName ?? (rule?.PropertyNameMappings.TryGetValue(propName, out var alias) == true ? alias : propName);

                if (sourceProps.TryGetValue(sourceLookupName, out var sourceProp))
                {
                    if (sourceProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
                    {
                        continue;
                    }

                    var rawVal = sourceProp.GetValue(src);
                    var shouldIgnoreNull = (rule?.IgnoreNullValues == true) ||
                                           sourceType.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null ||
                                           sourceProp.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null ||
                                           targetProp.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null;

                    if (shouldIgnoreNull && rawVal is null)
                    {
                        continue;
                    }

                    var mappedVal = MapValue(rawVal, sourceProp.PropertyType, targetProp.PropertyType, ctx, mapper);
                    targetProp.SetValue(targetInstance, mappedVal);
                }
                else if (_configuration.EnableFlattening &&
                         KyrolusMemberFlatteningResolver.ResolveFlattenedPath(sourceType, propName) is { } path)
                {
                    var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
                    var shouldIgnoreNull = (rule?.IgnoreNullValues == true) ||
                                           sourceType.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null ||
                                           targetProp.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null;

                    if (shouldIgnoreNull && rawVal is null)
                    {
                        continue;
                    }

                    var mappedVal = MapValue(rawVal, path.Last().PropertyType, targetProp.PropertyType, ctx, mapper);
                    targetProp.SetValue(targetInstance, mappedVal);
                }
            }

            // AfterMap hooks
            if (rule?.AfterMapActions.Count > 0)
            {
                foreach (var after in rule.AfterMapActions)
                {
                    after(src, targetInstance, ctx);
                }
            }
        };
    }

    private object? MapValue(object? value, Type sourceType, Type targetType, KyrolusMappingContext context, IKyrolusObjectMapper mapper)
    {
        if (value is null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        // Direct matching types
        if (targetType.IsAssignableFrom(sourceType))
        {
            return value;
        }

        // Nullable unwrap
        var underlyingTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var underlyingSource = Nullable.GetUnderlyingType(sourceType) ?? sourceType;

        if (underlyingTarget.IsAssignableFrom(underlyingSource))
        {
            return value;
        }

        // String conversions
        if (targetType == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        // Enum conversions
        if (underlyingTarget.IsEnum)
        {
            if (value is string str)
            {
                return Enum.Parse(underlyingTarget, str, ignoreCase: true);
            }

            return Enum.ToObject(underlyingTarget, value);
        }

        if (underlyingSource.IsEnum && underlyingTarget == typeof(string))
        {
            return value.ToString();
        }

        // Guid conversions
        if (underlyingTarget == typeof(Guid) && value is string guidStr)
        {
            return Guid.Parse(guidStr);
        }

        if (underlyingSource == typeof(Guid) && underlyingTarget == typeof(string))
        {
            return value.ToString();
        }

        // DateTime / DateTimeOffset conversions
        if (underlyingTarget == typeof(DateTimeOffset) && value is DateTime dt)
        {
            if (dt == DateTime.MinValue)
            {
                return DateTimeOffset.MinValue;
            }

            if (dt == DateTime.MaxValue)
            {
                return DateTimeOffset.MaxValue;
            }

            return dt.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(dt, TimeSpan.Zero)
                : new DateTimeOffset(dt);
        }

        if (underlyingTarget == typeof(DateTime) && value is DateTimeOffset dto)
        {
            return dto.DateTime;
        }

        // Primitive Convert.ChangeType
        if (typeof(IConvertible).IsAssignableFrom(underlyingSource) && typeof(IConvertible).IsAssignableFrom(underlyingTarget))
        {
            try
            {
                return Convert.ChangeType(value, underlyingTarget, CultureInfo.InvariantCulture);
            }
            catch
            {
                // Fallback to recursive mapping
            }
        }

        // Complex / nested object recursion
        return Map(underlyingSource, underlyingTarget, value, context, mapper);
    }
}
