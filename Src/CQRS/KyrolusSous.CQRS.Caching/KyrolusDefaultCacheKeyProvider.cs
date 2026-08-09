using System.Reflection;

namespace KyrolusSous.CQRS.Caching;

public sealed class KyrolusDefaultCacheKeyProvider : IKyrolusCacheKeyProvider
{
    public string? GetCacheKey(object request)
    {
        if (request is null)
        {
            return null;
        }

        var explicitKey = TryGetStringProperty(request, "CacheKey");
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey;
        }

        var entityName = ResolveEntityTypeName(request);
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return null;
        }

        var requestName = request.GetType().Name;
        if (requestName.StartsWith("GetAll", StringComparison.Ordinal))
        {
            return $"{entityName}_GetAll";
        }

        if (requestName.StartsWith("GetById", StringComparison.Ordinal) || requestName.Contains("ById", StringComparison.Ordinal))
        {
            var id = TryGetPropertyValue(request, "Id") ?? TryGetPropertyValue(request, "Key");
            if (id is not null)
            {
                return $"{entityName}_GetById_{id}";
            }
        }

        return $"{entityName}_{requestName}";
    }

    public string? GetCachePattern(object request)
    {
        if (request is null)
        {
            return null;
        }

        return ResolveEntityTypeName(request);
    }

    private static string? TryGetStringProperty(object request, string propertyName)
    {
        var prop = request.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop?.PropertyType != typeof(string))
        {
            return null;
        }

        return prop.GetValue(request) as string;
    }

    private static object? TryGetPropertyValue(object request, string propertyName)
    {
        var prop = request.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(request);
    }

    private static string? ResolveEntityTypeName(object request)
    {
        var responseType = TryGetResponseType(request);
        if (responseType is null)
        {
            return null;
        }

        var entityType = UnwrapEntityType(responseType);
        return entityType?.Name;
    }

    private static Type? TryGetResponseType(object request)
    {
        var requestType = request.GetType();
        foreach (var iface in requestType.GetInterfaces())
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var def = iface.GetGenericTypeDefinition();
            if (def == typeof(IKyrolusQuery<>) || def == typeof(IKyrolusCommand<>))
            {
                return iface.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static Type? UnwrapEntityType(Type responseType)
    {
        if (!responseType.IsGenericType)
        {
            return responseType;
        }

        var def = responseType.GetGenericTypeDefinition();
        if (def == typeof(IEnumerable<>) || def == typeof(List<>))
        {
            return responseType.GetGenericArguments()[0];
        }

        if (responseType.GetGenericArguments().Length == 1)
        {
            return responseType.GetGenericArguments()[0];
        }

        return responseType;
    }
}
