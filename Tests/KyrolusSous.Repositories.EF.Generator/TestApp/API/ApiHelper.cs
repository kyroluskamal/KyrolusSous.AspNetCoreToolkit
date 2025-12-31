using System.Collections;
using System.Reflection;

namespace KyrolusSous.Repositories.EF.Generator.TestApp.API;

public static class ApiHelper
{
    public static object ShapeForResponse<TEntity>(IEnumerable<TEntity> items, string[]? includes)
    {
        var inc = includes ?? Array.Empty<string>();

        return items.Select(e =>
        {
            var o = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;

                var t = p.PropertyType;
                var isEnumerable = t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t);

                if (isEnumerable) continue;
                if (!t.IsValueType && t != typeof(string)) continue;

                o[ToCamel(p.Name)] = p.GetValue(e);
            }

            foreach (var path in inc)
            {
                o[ToCamel(path.Replace(".", "_"))] = GetByPath(e!, path);
            }

            return o;
        });
    }

    static object? GetByPath(object obj, string path)
    {
        object? cur = obj;
        foreach (var seg in path.Split('.'))
        {
            if (cur is null) return null;
            var prop = cur.GetType().GetProperty(seg, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null) return null;
            cur = prop.GetValue(cur);
        }
        return cur;
    }

    static string ToCamel(string s) => char.ToLowerInvariant(s[0]) + s[1..];
}