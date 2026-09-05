namespace KyrolusSous.Mapping.Runtime.Engine;

/// <summary>
/// Core dynamic mapping engine that analyzes type pairs and executes high-speed compiled mappings.
/// </summary>
public sealed class KyrolusExpressionMappingEngine
{
    private readonly KyrolusMappingConfiguration _configuration;
    private readonly ConcurrentDictionary<(Type Source, Type Target), TypeMappingPlan> _planCache = new();

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

        var plan = _planCache.GetOrAdd((sourceType, targetType), key => new TypeMappingPlan(key.Source, key.Target, _configuration));
        return plan.Execute(source, context, mapper, this);
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

        var plan = _planCache.GetOrAdd((sourceType, targetType), key => new TypeMappingPlan(key.Source, key.Target, _configuration));
        plan.ExecuteInPlace(source, target, context, mapper, this);
    }

    private static bool IsDirectlyAssignable(Type sourceType, Type targetType)
    {
        // Unwrap Nullable<T> so a nullable primitive/enum/struct round-trips through the fast path the
        // same way its non-nullable form does, instead of falling through to reflection-based
        // TypeMappingPlan (which finds no same-named properties on a scalar and silently produces a
        // zeroed default).
        var underlyingSource = Nullable.GetUnderlyingType(sourceType) ?? sourceType;

        var isScalarKind =
            underlyingSource == typeof(string) ||
            underlyingSource.IsPrimitive ||
            underlyingSource.IsEnum ||
            underlyingSource == typeof(decimal) ||
            underlyingSource == typeof(Guid) ||
            underlyingSource == typeof(DateTime) ||
            underlyingSource == typeof(DateTimeOffset) ||
            underlyingSource == typeof(TimeSpan) ||
            underlyingSource == typeof(DateOnly) ||
            underlyingSource == typeof(TimeOnly);

        return (isScalarKind || targetType == typeof(object)) && targetType.IsAssignableFrom(sourceType);
    }

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

    internal object? MapValue(object? value, Type sourceType, Type targetType, KyrolusMappingContext context, IKyrolusObjectMapper mapper)
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

    private sealed class TypeMappingPlan
    {
        private readonly Type _sourceType;
        private readonly Type _targetType;
        private readonly ConstructorInfo? _constructor;
        private readonly KyrolusTypeMappingRule? _rule;
        private readonly Dictionary<string, PropertyInfo> _sourceProps;
        private readonly List<PropertyInfo> _targetProps;
        private readonly KyrolusMappingConfiguration _configuration;

        public TypeMappingPlan(Type sourceType, Type targetType, KyrolusMappingConfiguration configuration)
        {
            _sourceType = sourceType;
            _targetType = targetType;
            _configuration = configuration;
            _rule = configuration.FindRule(sourceType, targetType);
            _constructor = ResolveConstructor(targetType);
            _sourceProps = GetReadableProperties(sourceType);
            _targetProps = GetWritableProperties(targetType);
        }

        public object Execute(object src, KyrolusMappingContext ctx, IKyrolusObjectMapper mapper, KyrolusExpressionMappingEngine engine)
        {
            var boundProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetInstance = CreateTargetInstance(src, ctx, mapper, engine, boundProps);

            TrackCircularReference(src, targetInstance, ctx);
            ExecuteHooks(_rule?.BeforeMapActions, src, targetInstance, ctx);

            MapWritableProperties(src, targetInstance, ctx, mapper, engine, boundProps);
            ExecuteHooks(_rule?.AfterMapActions, src, targetInstance, ctx);

            return targetInstance;
        }

        public void ExecuteInPlace(object src, object targetInstance, KyrolusMappingContext ctx, IKyrolusObjectMapper mapper, KyrolusExpressionMappingEngine engine)
        {
            TrackCircularReference(src, targetInstance, ctx);
            ExecuteHooks(_rule?.BeforeMapActions, src, targetInstance, ctx);

            MapWritablePropertiesInPlace(src, targetInstance, ctx, mapper, engine);
            ExecuteHooks(_rule?.AfterMapActions, src, targetInstance, ctx);
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
            object src,
            KyrolusMappingContext ctx,
            IKyrolusObjectMapper mapper,
            KyrolusExpressionMappingEngine engine,
            HashSet<string> boundProps)
        {
            if (_rule?.CustomConstructor is not null)
            {
                return _rule.CustomConstructor.DynamicInvoke(src)!;
            }

            if (_constructor is not null && _constructor.GetParameters().Length > 0)
            {
                var args = BuildConstructorArgs(src, ctx, mapper, engine, boundProps);
                return _constructor.Invoke(args);
            }

            return Activator.CreateInstance(_targetType)!;
        }

        private object?[] BuildConstructorArgs(
            object src,
            KyrolusMappingContext ctx,
            IKyrolusObjectMapper mapper,
            KyrolusExpressionMappingEngine engine,
            HashSet<string> boundProps)
        {
            var parameters = _constructor!.GetParameters();
            var args = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramName = param.Name ?? string.Empty;
                boundProps.Add(paramName);

                args[i] = ResolveParamValue(param, paramName, src, ctx, mapper, engine);
            }

            return args;
        }

        private object? ResolveParamValue(
            ParameterInfo param,
            string paramName,
            object src,
            KyrolusMappingContext ctx,
            IKyrolusObjectMapper mapper,
            KyrolusExpressionMappingEngine engine)
        {
            if (_rule?.CustomMemberResolvers.TryGetValue(paramName, out var customResolver) == true)
            {
                return customResolver(src, ctx);
            }

            if (_sourceProps.TryGetValue(paramName, out var matchedSourceProp))
            {
                var rawVal = matchedSourceProp.GetValue(src);
                return engine.MapValue(rawVal, matchedSourceProp.PropertyType, param.ParameterType, ctx, mapper);
            }

            if (_configuration.EnableFlattening &&
                KyrolusMemberFlatteningResolver.ResolveFlattenedPath(_sourceType, paramName) is { } path)
            {
                var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
                return engine.MapValue(rawVal, path.Last().PropertyType, param.ParameterType, ctx, mapper);
            }

            if (param.HasDefaultValue)
            {
                return param.DefaultValue;
            }

            return param.ParameterType.IsValueType
                ? Activator.CreateInstance(param.ParameterType)
                : null;
        }

        private void MapWritableProperties(
            object src,
            object targetInstance,
            KyrolusMappingContext ctx,
            IKyrolusObjectMapper mapper,
            KyrolusExpressionMappingEngine engine,
            HashSet<string> boundProps)
        {
            foreach (var targetProp in _targetProps)
            {
                if (boundProps.Contains(targetProp.Name))
                {
                    continue;
                }

                MapSingleProperty(targetProp, src, targetInstance, ctx, mapper, engine);
            }
        }

        private void MapSingleProperty(
            PropertyInfo targetProp,
            object src,
            object targetInstance,
            KyrolusMappingContext ctx,
            IKyrolusObjectMapper mapper,
            KyrolusExpressionMappingEngine engine)
        {
            var propName = targetProp.Name;

            if (IsPropertyIgnored(targetProp, propName))
            {
                return;
            }

            if (_rule?.MemberConditions.TryGetValue(propName, out var condition) == true && !condition(src, ctx))
            {
                return;
            }

            if (_rule?.CustomMemberResolvers.TryGetValue(propName, out var customResolver) == true)
            {
                targetProp.SetValue(targetInstance, customResolver(src, ctx));
                return;
            }

            var sourceLookupName = ResolveSourcePropertyName(targetProp, propName);

            if (_sourceProps.TryGetValue(sourceLookupName, out var sourceProp))
            {
                if (sourceProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
                {
                    return;
                }

                var rawVal = sourceProp.GetValue(src);
                var mappedVal = engine.MapValue(rawVal, sourceProp.PropertyType, targetProp.PropertyType, ctx, mapper);
                targetProp.SetValue(targetInstance, mappedVal);
            }
            else if (_configuration.EnableFlattening &&
                     KyrolusMemberFlatteningResolver.ResolveFlattenedPath(_sourceType, propName) is { } path)
            {
                var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
                var mappedVal = engine.MapValue(rawVal, path.Last().PropertyType, targetProp.PropertyType, ctx, mapper);
                targetProp.SetValue(targetInstance, mappedVal);
            }
        }

        private void MapWritablePropertiesInPlace(
            object src,
            object targetInstance,
            KyrolusMappingContext ctx,
            IKyrolusObjectMapper mapper,
            KyrolusExpressionMappingEngine engine)
        {
            foreach (var targetProp in _targetProps)
            {
                MapSinglePropertyInPlace(targetProp, src, targetInstance, ctx, mapper, engine);
            }
        }

        private void MapSinglePropertyInPlace(
            PropertyInfo targetProp,
            object src,
            object targetInstance,
            KyrolusMappingContext ctx,
            IKyrolusObjectMapper mapper,
            KyrolusExpressionMappingEngine engine)
        {
            var propName = targetProp.Name;

            if (IsPropertyIgnored(targetProp, propName))
            {
                return;
            }

            if (_rule?.MemberConditions.TryGetValue(propName, out var condition) == true && !condition(src, ctx))
            {
                return;
            }

            if (_rule?.CustomMemberResolvers.TryGetValue(propName, out var customResolver) == true)
            {
                targetProp.SetValue(targetInstance, customResolver(src, ctx));
                return;
            }

            var sourceLookupName = ResolveSourcePropertyName(targetProp, propName);

            if (_sourceProps.TryGetValue(sourceLookupName, out var sourceProp))
            {
                if (sourceProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null)
                {
                    return;
                }

                var rawVal = sourceProp.GetValue(src);
                if (ShouldIgnoreNull(sourceProp, targetProp, rawVal, ctx))
                {
                    return;
                }

                var mappedVal = engine.MapValue(rawVal, sourceProp.PropertyType, targetProp.PropertyType, ctx, mapper);
                targetProp.SetValue(targetInstance, mappedVal);
            }
            else if (_configuration.EnableFlattening &&
                     KyrolusMemberFlatteningResolver.ResolveFlattenedPath(_sourceType, propName) is { } path)
            {
                var rawVal = KyrolusMemberFlatteningResolver.EvaluatePath(path, src);
                if (ShouldIgnoreNull(null, targetProp, rawVal, ctx))
                {
                    return;
                }

                var mappedVal = engine.MapValue(rawVal, path.Last().PropertyType, targetProp.PropertyType, ctx, mapper);
                targetProp.SetValue(targetInstance, mappedVal);
            }
        }

        private bool IsPropertyIgnored(PropertyInfo targetProp, string propName) =>
            (_rule?.IgnoredMembers.Contains(propName) == true) ||
            targetProp.GetCustomAttribute<KyrolusIgnoreMapAttribute>() is not null;

        private string ResolveSourcePropertyName(PropertyInfo targetProp, string propName)
        {
            var mapAttr = targetProp.GetCustomAttribute<KyrolusMapPropertyAttribute>();
            return mapAttr?.SourceName ?? (_rule?.PropertyNameMappings.TryGetValue(propName, out var alias) == true ? alias : propName);
        }

        private bool ShouldIgnoreNull(PropertyInfo? sourceProp, PropertyInfo targetProp, object? rawVal, KyrolusMappingContext ctx)
        {
            if (rawVal is not null)
            {
                return false;
            }

            return (_rule?.IgnoreNullValues == true) ||
                   ctx.GetItem<bool>(KyrolusMappingContext.IgnoreNullValuesOnInPlaceMapKey) ||
                   _sourceType.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null ||
                   (sourceProp is not null && sourceProp.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null) ||
                   targetProp.GetCustomAttribute<KyrolusIgnoreNullAttribute>() is not null;
        }

        private void TrackCircularReference(object src, object targetInstance, KyrolusMappingContext ctx)
        {
            if (_configuration.EnableCircularReferenceTracking && !_sourceType.IsValueType && !_targetType.IsValueType)
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
    }
}
