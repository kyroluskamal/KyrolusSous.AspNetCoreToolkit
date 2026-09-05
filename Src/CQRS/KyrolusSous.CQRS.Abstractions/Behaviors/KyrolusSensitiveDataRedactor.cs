using System.Collections;
using System.Reflection;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Recursively redacts sensitive-looking property and dictionary-key names out of an object graph
/// before it is handed to a destination outside the current call.
/// </summary>
/// <remarks>
/// Shared by every pipeline behavior in this namespace that exposes a request/response payload beyond
/// the call that produced it - <see cref="KyrolusAuditBehavior{TRequest,TResponse}"/> (an audit sink)
/// and <see cref="KyrolusLivePushBehavior{TRequest,TResponse}"/> (real-time subscribers). It used to
/// live only inside <c>KyrolusAuditBehavior</c>: <c>KyrolusLivePushBehavior</c> broadcast
/// <c>PushData</c>/response/request objects to real-time subscribers completely unredacted - a more
/// exposed destination than an audit sink (arbitrary connected clients vs. a comparatively trusted log
/// or database), with no equivalent protection. Extracting the keyword list and the recursive walk here
/// closes that gap and keeps both behaviors' redaction rules from drifting apart independently.
/// </remarks>
internal static class KyrolusSensitiveDataRedactor
{
    /// <summary>Bounds recursion into nested objects - deep enough for realistic DTO graphs, shallow enough that a self-referencing or pathological graph cannot recurse indefinitely.</summary>
    private const int MaxSanitizeDepth = 6;

    internal const string RedactedPlaceholder = "***REDACTED***";
    internal const string UnavailablePlaceholder = "***UNAVAILABLE***";

    /// <summary>
    /// Format used in place of a <c>byte[]</c>/<c>Memory&lt;byte&gt;</c>/<c>ReadOnlyMemory&lt;byte&gt;</c>
    /// property's actual bytes - see the <see cref="IsSimpleType"/> remarks for why raw binary content
    /// is never emitted here.
    /// </summary>
    private const string BinaryPlaceholderFormat = "<binary data, {0} bytes>";

    private static readonly string[] BuiltInSensitiveKeywords =
    [
        "password", "secret", "token", "pin", "cvv", "cardnumber", "apikey"
    ];

    /// <summary>
    /// Returns a redacted copy of <paramref name="payload"/>. A property or dictionary key whose name
    /// contains a built-in or <paramref name="extraSensitiveKeywords"/> keyword (case-insensitive) is
    /// replaced with a redaction placeholder; everything else is walked recursively - into nested
    /// objects, dictionaries and enumerables - and returned as-is.
    /// </summary>
    public static object? Sanitize(object? payload, IReadOnlyList<string>? extraSensitiveKeywords = null)
        => Sanitize(payload, extraSensitiveKeywords ?? [], depth: 0);

    private static object? Sanitize(object? payload, IReadOnlyList<string> extraKeywords, int depth)
    {
        if (payload is null) return null;

        // A nested DTO's own sensitive properties (a PaymentDetails.CardNumber inside an order
        // command, say) must be redacted the same as a top-level one - only inspecting top-level
        // property names would pass nested sensitive data straight through to whatever the caller
        // does with it, defeating the point of sanitizing at all.
        if (depth >= MaxSanitizeDepth || IsSimpleType(payload.GetType()))
        {
            // byte[]/Memory<byte>/ReadOnlyMemory<byte> are classified as "simple" so they are never
            // handed to SanitizeEnumerable below (see IsSimpleType's remarks for why that matters),
            // but a simple type is otherwise returned as-is - which for binary data would still mean
            // returning the raw bytes verbatim to an audit sink or live-push subscriber. That is both
            // needless log/payload bloat for anything of meaningful size and potentially sensitive in
            // its own right (an encrypted blob, a document, an image), so it gets a short descriptive
            // placeholder instead, the same way a sensitive-named property does.
            return payload switch
            {
                byte[] bytes => string.Format(BinaryPlaceholderFormat, bytes.Length),
                ReadOnlyMemory<byte> readOnlyMemory => string.Format(BinaryPlaceholderFormat, readOnlyMemory.Length),
                Memory<byte> memory => string.Format(BinaryPlaceholderFormat, memory.Length),
                _ => payload
            };
        }

        // A dictionary (e.g. the Updates bag on a Patch/BulkPatch/ExecuteUpdate command) is also
        // IEnumerable<KeyValuePair<,>>, so this check must run before the general IEnumerable branch
        // below - otherwise each entry gets reflected as a KeyValuePair and IsSensitive is checked
        // against the literal names "Key"/"Value" instead of the entry's actual key (e.g. "Password"),
        // and the real key never gets redacted at all.
        if (payload is IDictionary dictionary)
            return SanitizeDictionary(dictionary, extraKeywords, depth);

        if (payload is IEnumerable enumerable and not string)
            return SanitizeEnumerable(enumerable, extraKeywords, depth);

        return SanitizeObject(payload, extraKeywords, depth);
    }

    private static List<object?> SanitizeEnumerable(IEnumerable enumerable, IReadOnlyList<string> extraKeywords, int depth)
    {
        var items = new List<object?>();
        foreach (var item in enumerable)
        {
            // One pathological item (a lazy/computed value that throws on enumeration or on its own
            // sanitization) must not discard every other item already sanitized in this collection.
            try
            {
                items.Add(Sanitize(item, extraKeywords, depth + 1));
            }
            catch
            {
                items.Add(UnavailablePlaceholder);
            }
        }
        return items;
    }

    private static object SanitizeDictionary(IDictionary dictionary, IReadOnlyList<string> extraKeywords, int depth)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString() ?? "null";
            try
            {
                result[key] = IsSensitive(key, extraKeywords)
                    ? RedactedPlaceholder
                    : Sanitize(entry.Value, extraKeywords, depth + 1);
            }
            catch
            {
                result[key] = UnavailablePlaceholder;
            }
        }
        return result;
    }

    private static object SanitizeObject(object payload, IReadOnlyList<string> extraKeywords, int depth)
    {
        var props = payload.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (props.Length == 0) return payload;

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props)
        {
            if (prop.GetIndexParameters().Length > 0) continue; // skip indexers

            var name = prop.Name;
            if (IsSensitive(name, extraKeywords))
            {
                dict[name] = RedactedPlaceholder;
                continue;
            }

            // A single property whose getter throws (a computed property touching a disposed
            // DbContext navigation, say) must not force the entire object to fall back to being
            // exposed raw - that would re-expose every sensitive property already redacted above it.
            try
            {
                dict[name] = Sanitize(prop.GetValue(payload), extraKeywords, depth + 1);
            }
            catch
            {
                dict[name] = UnavailablePlaceholder;
            }
        }
        return dict;
    }

    /// <remarks>
    /// <c>byte[]</c> (and the <c>Memory&lt;byte&gt;</c>/<c>ReadOnlyMemory&lt;byte&gt;</c> shapes commonly
    /// used alongside it) is classified as simple/opaque rather than left to fall through to the
    /// general <see cref="IEnumerable"/> branch in <see cref="Sanitize(object?,IReadOnlyList{string},int)"/>.
    /// Without this, a binary property (a file upload, a thumbnail, an encrypted blob) would be routed
    /// into <see cref="SanitizeEnumerable"/>, which iterates ONE BYTE AT A TIME and boxes each into an
    /// <see cref="object"/> - for a multi-megabyte field that is millions of boxing allocations on
    /// every single execution of an auditable/live-pushed command, since <c>IncludePayload</c>/
    /// <c>PushData</c> default to including the full request. Treating it as simple short-circuits
    /// straight past both the dictionary and enumerable branches.
    /// </remarks>
    private static bool IsSimpleType(Type type)
        => type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(TimeSpan)
        || type == typeof(Guid)
        || type == typeof(Uri)
        || type == typeof(byte[])
        || type == typeof(ReadOnlyMemory<byte>)
        || type == typeof(Memory<byte>)
        || (Nullable.GetUnderlyingType(type) is { } underlying && IsSimpleType(underlying));

    private static bool IsSensitive(string name, IReadOnlyList<string> extraKeywords)
    {
        foreach (var keyword in BuiltInSensitiveKeywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        }

        foreach (var keyword in extraKeywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
