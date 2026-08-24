namespace KyrolusSous.Mapping.Runtime.Engine;

/// <summary>
/// Resolves flattened property hierarchies (e.g. mapping <c>Order.Customer.Address.City</c> to <c>OrderDto.CustomerAddressCity</c>).
/// </summary>
public static class KyrolusMemberFlatteningResolver
{
    private static readonly ConcurrentDictionary<(Type SourceType, string TargetName), PropertyInfo[]?> _cache = new();

    /// <summary>
    /// Attempts to find a sequence of nested properties on <paramref name="sourceType"/> matching the flattened <paramref name="targetMemberName"/>.
    /// </summary>
    /// <param name="sourceType">The source root type.</param>
    /// <param name="targetMemberName">The target property name (e.g. <c>"CustomerCity"</c>).</param>
    /// <returns>An array of nested property accessors if a valid path was found; otherwise, <c>null</c>.</returns>
    public static PropertyInfo[]? ResolveFlattenedPath(Type sourceType, string targetMemberName)
    {
        return _cache.GetOrAdd((sourceType, targetMemberName), static key =>
        {
            var (type, name) = key;
            var path = new List<PropertyInfo>();
            if (TryMatchPath(type, name, path))
            {
                return path.ToArray();
            }

            return null;
        });
    }

    private static bool TryMatchPath(Type currentType, string remainingName, List<PropertyInfo> path)
    {
        if (string.IsNullOrEmpty(remainingName))
        {
            return true;
        }

        var properties = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // First check for direct exact match
        var exact = properties.FirstOrDefault(p => p.Name.Equals(remainingName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            path.Add(exact);
            return true;
        }

        // Search for prefixes matching available properties
        foreach (var prop in properties)
        {
            if (remainingName.StartsWith(prop.Name, StringComparison.OrdinalIgnoreCase))
            {
                path.Add(prop);
                var nextRemaining = remainingName.Substring(prop.Name.Length);
                if (TryMatchPath(prop.PropertyType, nextRemaining, path))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates a resolved property path on a source instance, navigating safely through null values.
    /// </summary>
    public static object? EvaluatePath(PropertyInfo[] path, object? source)
    {
        var current = source;
        foreach (var prop in path)
        {
            if (current is null)
            {
                return null;
            }

            current = prop.GetValue(current);
        }

        return current;
    }
}
