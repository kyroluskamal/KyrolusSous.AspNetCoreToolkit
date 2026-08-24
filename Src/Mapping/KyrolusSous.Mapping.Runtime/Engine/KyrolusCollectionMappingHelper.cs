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

        if (type.IsGenericType)
        {
            var genType = type.GetGenericTypeDefinition();
            if (genType == typeof(IEnumerable<>) ||
                genType == typeof(IReadOnlyList<>) ||
                genType == typeof(IReadOnlyCollection<>) ||
                genType == typeof(IList<>) ||
                genType == typeof(ICollection<>) ||
                genType == typeof(List<>) ||
                genType == typeof(HashSet<>) ||
                genType == typeof(ISet<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
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
        Type sourceElementType,
        Type targetElementType,
        Func<object, KyrolusMappingContext, object?> elementMapper,
        KyrolusMappingContext context)
    {
        if (source is null)
        {
            return null;
        }

        var count = (source as ICollection)?.Count ?? (source as IReadOnlyCollection<object>)?.Count ?? 0;

        // If target is array: TTarget[]
        if (targetCollectionType.IsArray)
        {
            var list = new List<object?>(count);
            foreach (var item in source)
            {
                list.Add(item is null ? null : elementMapper(item, context));
            }

            var array = Array.CreateInstance(targetElementType, list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                array.SetValue(list[i], i);
            }

            return array;
        }

        // If target is HashSet<TTarget>
        if (targetCollectionType.IsGenericType && (targetCollectionType.GetGenericTypeDefinition() == typeof(HashSet<>) || targetCollectionType.GetGenericTypeDefinition() == typeof(ISet<>)))
        {
            var hashSetType = typeof(HashSet<>).MakeGenericType(targetElementType);
            var hashSet = Activator.CreateInstance(hashSetType)!;
            var addMethod = hashSetType.GetMethod("Add")!;

            foreach (var item in source)
            {
                var mapped = item is null ? null : elementMapper(item, context);
                addMethod.Invoke(hashSet, [mapped]);
            }

            return hashSet;
        }

        // Default to List<TTarget> for IEnumerable<T>, IList<T>, IReadOnlyList<T>, List<T>
        var targetListType = typeof(List<>).MakeGenericType(targetElementType);
        var targetList = (IList)Activator.CreateInstance(targetListType, count)!;

        foreach (var item in source)
        {
            var mapped = item is null ? null : elementMapper(item, context);
            targetList.Add(mapped);
        }

        return targetList;
    }
}
