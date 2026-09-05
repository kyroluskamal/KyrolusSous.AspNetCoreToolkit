using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace KyrolusSous.CQRS.Caching;

public sealed class KyrolusDefaultCacheKeyProvider : IKyrolusCacheKeyProvider
{
    /// <summary>
    /// Verb prefixes stripped from a command's own type name by <see cref="DeriveEntityNameFromCommandName"/>,
    /// checked in this order. Matches this codebase's actual command naming conventions (see the
    /// EF/Marten <c>Command</c> folders and <see cref="KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusCommand{TResponse}"/>'s
    /// own <c>CreateUser</c> example).
    /// </summary>
    private static readonly string[] CommandNamePrefixes =
    [
        "Create", "Update", "Delete", "Remove", "Patch", "Upsert", "Execute", "SoftDelete", "Restore", "Add"
    ];

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

        var pageNumber = TryGetPropertyValue(request, "PageNumber");
        var pageSize = TryGetPropertyValue(request, "PageSize");
        if (pageNumber is not null && pageSize is not null)
        {
            return $"{entityName}_{requestName}_p{pageNumber}_s{pageSize}";
        }

        var cursor = TryGetPropertyValue(request, "Cursor") ?? TryGetPropertyValue(request, "NextToken");
        if (cursor is not null)
        {
            return $"{entityName}_{requestName}_c{cursor}";
        }

        // None of the shapes above apply - this is some other filtered query (e.g.
        // GetOrdersByStatusQuery(string Status)) whose cache key must still depend on whatever makes
        // one instance different from another, or every instance of the request type collapses onto
        // this same "{entityName}_{requestName}" key regardless of the actual filter values, and the
        // second caller silently gets served the first caller's (wrong) cached result. Folding every
        // public property's value into the key is a blunt but reliable way to make that happen without
        // knowing which property matters for any given request type.
        return $"{entityName}_{requestName}_{BuildPropertiesFingerprint(request)}";
    }

    public string? GetCachePattern(object request)
    {
        if (request is null)
        {
            return null;
        }

        // An explicit override always wins over the naming-convention heuristic below - the reliable
        // way to guarantee correct invalidation for a command whose name does not follow the
        // verb+entity+"Command" convention DeriveEntityNameFromCommandName assumes.
        var explicitPattern = TryGetStringProperty(request, "InvalidatesCachePattern");
        if (!string.IsNullOrWhiteSpace(explicitPattern))
        {
            return explicitPattern;
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

    /// <summary>
    /// Renders every public instance property of <paramref name="request"/> as a deterministic
    /// <c>Name=Value</c> fingerprint, ordered by property name so the same request shape always
    /// produces the same fingerprint regardless of reflection's enumeration order.
    /// </summary>
    /// <remarks>
    /// This is a cache key, not a display string - primitives/strings/enums/<see cref="Guid"/>/date
    /// types are rendered with <see cref="Convert.ToString(object?, IFormatProvider?)"/>, and anything
    /// else (a nested object, a collection) falls back to a plain JSON serialization. Neither needs to
    /// be pretty, only stable for a given value.
    /// </remarks>
    private static string BuildPropertiesFingerprint(object request)
    {
        var properties = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

        return string.Join(";", properties.Select(p => $"{p.Name}={FormatPropertyValue(p.GetValue(request))}"));
    }

    private static string FormatPropertyValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        return value switch
        {
            string s => s,
            bool or byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal or char or Guid or DateTime or DateTimeOffset or Enum
                => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null",
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static string? ResolveEntityTypeName(object request)
    {
        var requestType = request.GetType();
        var responseType = TryGetResponseType(requestType);
        var entityType = responseType is null ? null : UnwrapEntityType(responseType);

        // A command's TResponse frequently is not the entity at all - IKyrolusCommand<Guid> (the new
        // id), IKyrolusCommand<bool> (a success flag), or no TResponse whatsoever for a plain
        // IKyrolusCommand. Reflecting straight off that response type would resolve to "Guid",
        // "Boolean", or nothing usable - none of which match the "{Entity}_*" keys
        // KyrolusQueryCachingBehavior actually writes, so KyrolusCommandCacheInvalidationBehavior's
        // wildcard pattern would never match anything and invalidation would silently do nothing.
        // Falling back to the naming-convention heuristic on the command's own type name is a better
        // (if still best-effort) default for exactly those shapes.
        if (request is IKyrolusCommandBase && (entityType is null || IsScalarLikeType(entityType)))
        {
            return DeriveEntityNameFromCommandName(requestType);
        }

        return entityType?.Name;
    }

    private static Type? TryGetResponseType(Type requestType)
    {
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

    /// <summary>
    /// Whether <paramref name="type"/> looks like a scalar/system value (an id, a flag, a timestamp)
    /// rather than a domain entity or DTO - the signal <see cref="ResolveEntityTypeName"/> uses to
    /// decide a command's <c>TResponse</c> is not usable as-is for an invalidation pattern.
    /// </summary>
    private static bool IsScalarLikeType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(Guid)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Unit);
    }

    /// <summary>
    /// Best-effort fallback for deriving an entity name from a command's own type name (e.g.
    /// <c>CreateOrderCommand</c> -&gt; <c>Order</c>) for use as an invalidation pattern, when there is
    /// no explicit <c>InvalidatesCachePattern</c> override and no usable entity type to reflect off.
    /// </summary>
    /// <remarks>
    /// This is a naming-convention heuristic, not a guarantee: it strips the first matching prefix
    /// from <see cref="CommandNamePrefixes"/> and a trailing "Command" suffix, and simply returns the
    /// command's own type name unchanged if neither is present - so a pattern can still be derived
    /// (and invalidation still fires against at least something) rather than <see cref="GetCachePattern"/>
    /// returning null outright. A command named outside these conventions should set
    /// <c>InvalidatesCachePattern</c> explicitly rather than rely on this guess.
    /// </remarks>
    private static string DeriveEntityNameFromCommandName(Type requestType)
    {
        var name = requestType.Name;

        foreach (var prefix in CommandNamePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
            {
                name = name[prefix.Length..];
                break;
            }
        }

        const string CommandSuffix = "Command";
        if (name.EndsWith(CommandSuffix, StringComparison.Ordinal) && name.Length > CommandSuffix.Length)
        {
            name = name[..^CommandSuffix.Length];
        }

        return string.IsNullOrWhiteSpace(name) ? requestType.Name : name;
    }
}
