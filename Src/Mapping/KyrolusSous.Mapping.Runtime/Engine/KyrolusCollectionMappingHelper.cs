namespace KyrolusSous.Mapping.Runtime.Engine;

/// <summary>
/// Provides optimized, pre-allocated collection conversion routines across arrays, lists, hash sets, and read-only spans.
/// </summary>
public static class KyrolusCollectionMappingHelper
{
    /// <summary>
    /// Checks whether the specified type represents a non-string collection or enumerable.
    /// </summary>
    public static bool IsCollectionType(Type type, [NotNullWhen(true)] out Type? elementType)
    {
        if (type == typeof(string) || typeof(IDictionary).IsAssignableFrom(type))
        {
            elementType = null;
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType && IsStandardGenericCollection(type.GetGenericTypeDefinition()))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        var enumInterface = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumInterface is not null)
        {
            elementType = enumInterface.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    /// <summary>
    /// Maps an enumerable source collection into a destination collection of the specified type.
    /// </summary>
    public static object? MapCollection(
        IEnumerable? source,
        Type targetCollectionType,
        Type targetElementType,
        Func<object, KyrolusMappingContext, object?> elementMapper,
        KyrolusMappingContext context)
    {
        if (source is null)
        {
            return null;
        }

        var count = GetEstimatedCount(source);

        if (targetCollectionType.IsArray)
        {
            return MapToArray(source, targetElementType, elementMapper, context, count);
        }

        if (IsSetType(targetCollectionType))
        {
            return MapToHashSet(source, targetElementType, elementMapper, context);
        }

        return MapToList(source, targetElementType, elementMapper, context, count);
    }

    private static bool IsStandardGenericCollection(Type genType) =>
        genType == typeof(IEnumerable<>) ||
        genType == typeof(IReadOnlyList<>) ||
        genType == typeof(IReadOnlyCollection<>) ||
        genType == typeof(IList<>) ||
        genType == typeof(ICollection<>) ||
        genType == typeof(List<>) ||
        genType == typeof(HashSet<>) ||
        genType == typeof(ISet<>);

    private static bool IsSetType(Type type) =>
        type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(HashSet<>) || type.GetGenericTypeDefinition() == typeof(ISet<>));

    private static int GetEstimatedCount(IEnumerable source) =>
        (source as ICollection)?.Count ?? (source as IReadOnlyCollection<object>)?.Count ?? 0;

    private static Array MapToArray(
        IEnumerable source,
        Type targetElementType,
        Func<object, KyrolusMappingContext, object?> elementMapper,
        KyrolusMappingContext context,
        int estimatedCount)
    {
        var list = new List<object?>(estimatedCount);
        foreach (var item in source)
        {
            list.Add(MapElement(item, elementMapper, context));
        }

        var array = Array.CreateInstance(targetElementType, list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            array.SetValue(list[i], i);
        }

        return array;
    }

    private static object MapToHashSet(
        IEnumerable source,
        Type targetElementType,
        Func<object, KyrolusMappingContext, object?> elementMapper,
        KyrolusMappingContext context)
    {
        var hashSetType = typeof(HashSet<>).MakeGenericType(targetElementType);
        var hashSet = Activator.CreateInstance(hashSetType)!;
        var addMethod = hashSetType.GetMethod("Add")!;

        foreach (var item in source)
        {
            var mapped = MapElement(item, elementMapper, context);
            addMethod.Invoke(hashSet, [mapped]);
        }

        return hashSet;
    }

    private static IList MapToList(
        IEnumerable source,
        Type targetElementType,
        Func<object, KyrolusMappingContext, object?> elementMapper,
        KyrolusMappingContext context,
        int estimatedCount)
    {
        var targetListType = typeof(List<>).MakeGenericType(targetElementType);
        var targetList = (IList)Activator.CreateInstance(targetListType, estimatedCount)!;

        foreach (var item in source)
        {
            var mapped = MapElement(item, elementMapper, context);
            targetList.Add(mapped);
        }

        return targetList;
    }

    private static object? MapElement(
        object? item,
        Func<object, KyrolusMappingContext, object?> elementMapper,
        KyrolusMappingContext context) =>
        item is null ? null : elementMapper(item, context);
}
