namespace KyrolusSous.EndpointKit.EF;

public static class KyrolusSousRoutingHelpers
{
    public static List<string>? GetIncludedProperties(
        string? includedProperties,
        ISet<string>? allowlist,
        bool strict,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(includedProperties))
        {
            return null;
        }

        return GetIncludedProperties(includedProperties.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            allowlist, strict, out error);
    }

    public static List<string>? GetIncludedProperties(
        IEnumerable<string>? includeProperties,
        ISet<string>? allowlist,
        bool strict,
        out string? error)
    {
        error = null;
        if (includeProperties is null)
        {
            return null;
        }

        var allowed = NormalizeAllowlist(allowlist);
        var list = new List<string>();
        foreach (var include in includeProperties)
        {
            if (string.IsNullOrWhiteSpace(include)) continue;
            if (allowed is not null && !allowed.Contains(include))
            {
                if (strict)
                {
                    error = $"Include '{include}' is not allowed.";
                    return null;
                }
                continue;
            }
            list.Add(include);
        }

        return list.Count == 0 ? null : list;
    }

    private static ISet<string>? NormalizeAllowlist(ISet<string>? allowlist)
        => allowlist is null || allowlist.Count == 0 ? null : new HashSet<string>(allowlist, StringComparer.OrdinalIgnoreCase);
}
