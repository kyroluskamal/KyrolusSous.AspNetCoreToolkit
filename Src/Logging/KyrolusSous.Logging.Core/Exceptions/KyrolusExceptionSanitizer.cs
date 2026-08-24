using System.Collections;
using System.Text.RegularExpressions;
using KyrolusSous.Logging.Core.Redaction;

namespace KyrolusSous.Logging.Core.Exceptions;

/// <summary>
/// Sanitizes and unwraps exception hierarchies, removing sensitive credentials and flattening aggregate trees.
/// </summary>
public sealed partial class KyrolusExceptionSanitizer
{
    private readonly IKyrolusStringRedactor _stringRedactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusExceptionSanitizer"/> class.
    /// </summary>
    /// <param name="stringRedactor">Optional string redactor instance.</param>
    public KyrolusExceptionSanitizer(IKyrolusStringRedactor? stringRedactor = null)
    {
        _stringRedactor = stringRedactor ?? new KyrolusStringRedactor();
    }

    /// <summary>
    /// Sanitizes an exception message, removing database passwords, tokens, and sensitive query strings.
    /// </summary>
    /// <param name="message">The raw exception message.</param>
    /// <returns>The sanitized exception message.</returns>
    public string SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var result = ConnectionStringPasswordRegex().Replace(message, "$1***$3");
        return _stringRedactor.Redact(result);
    }

    /// <summary>
    /// Flattens and extracts all distinct root and nested exceptions in an aggregate hierarchy.
    /// </summary>
    /// <param name="exception">The root exception.</param>
    /// <returns>A flat list of distinct exceptions.</returns>
    public static IReadOnlyList<Exception> Flatten(Exception? exception)
    {
        if (exception is null)
        {
            return [];
        }

        var result = new List<Exception>();
        var queue = new Queue<Exception>();
        queue.Enqueue(exception);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            if (current is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    if (inner is not null)
                    {
                        queue.Enqueue(inner);
                    }
                }
            }
            else if (current.InnerException is not null)
            {
                queue.Enqueue(current.InnerException);
            }
        }

        return result;
    }

    /// <summary>
    /// Sanitizes an exception's Data dictionary, redacting sensitive keys or values.
    /// </summary>
    /// <param name="data">The exception's IDictionary data.</param>
    /// <returns>A sanitized dictionary suitable for structured logging.</returns>
    public IDictionary<string, object?> SanitizeData(IDictionary? data)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (data is null || data.Count == 0)
        {
            return sanitized;
        }

        foreach (DictionaryEntry entry in data)
        {
            var keyStr = entry.Key?.ToString() ?? "Unknown";
            var valStr = entry.Value?.ToString();

            sanitized[keyStr] = SanitizeMessage(valStr);
        }

        return sanitized;
    }

    [GeneratedRegex(@"(?i)((?:password|pwd|user id|uid|secret|token|api[_-]?key)\s*=\s*)([^;]+)(;?)", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex ConnectionStringPasswordRegex();
}
