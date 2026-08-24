using System.Runtime.CompilerServices;

namespace KyrolusSous.Repositories.EF.Runtime.Profiling;

/// <summary>
/// Provides automatic SQL query caller tagging extensions for APM, DBA tracing, and query profiling.
/// </summary>
public static class KyrolusQueryTaggingExtensions
{
    /// <summary>
    /// Tags the query with the calling member name, file path, and line number for instant SQL correlation.
    /// </summary>
    public static IQueryable<TEntity> TagWithCaller<TEntity>(
        this IQueryable<TEntity> source,
        string? customTag = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "Unknown";
        }

        var tag = string.IsNullOrWhiteSpace(customTag)
            ? $"Kyrolus: {memberName} [{fileName}:{lineNumber}]"
            : $"Kyrolus: {customTag} -> {memberName} [{fileName}:{lineNumber}]";

        return source.TagWith(tag);
    }
}
