using System.Collections;

namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Strongly-typed immutable representation of a route-level request/response transform in the API Gateway.
/// Implements <see cref="IReadOnlyDictionary{TKey, TValue}"/> for seamless backward compatibility with YARP's dictionary pipeline.
/// </summary>
public readonly record struct KyrolusGatewayTransform : IReadOnlyDictionary<string, string>, IEquatable<KyrolusGatewayTransform>
{
    private static readonly IReadOnlyDictionary<string, string> EmptyDict = new Dictionary<string, string>();
    private readonly IReadOnlyDictionary<string, string>? _values;

    /// <summary>
    /// Gets the underlying key-value transform configuration dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values => _values ?? EmptyDict;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusGatewayTransform"/> with the given dictionary values.
    /// </summary>
    /// <param name="values">The transform key-value pairs.</param>
    public KyrolusGatewayTransform(IReadOnlyDictionary<string, string> values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    #region Factory Methods - Path Transforms

    /// <summary>
    /// Strips the specified prefix from the request URL before proxying to the backend.
    /// </summary>
    /// <param name="prefix">The prefix path to remove (e.g. <c>"/api"</c>).</param>
    public static KyrolusGatewayTransform PathRemovePrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.PathRemovePrefix] = prefix
        });
    }

    /// <summary>
    /// Prepends the specified prefix to the request URL before proxying to the backend.
    /// </summary>
    /// <param name="prefix">The prefix path to prepend (e.g. <c>"/backend-v1"</c>).</param>
    public static KyrolusGatewayTransform PathPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.PathPrefix] = prefix
        });
    }

    /// <summary>
    /// Overwrites the request URL with a fixed static path before forwarding to the backend.
    /// </summary>
    /// <param name="path">The exact replacement path (e.g. <c>"/health"</c>).</param>
    public static KyrolusGatewayTransform PathSet(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.PathSet] = path
        });
    }

    /// <summary>
    /// Replaces the request URL using a template pattern (e.g. <c>"/api/v2/{**remainder}"</c>).
    /// </summary>
    /// <param name="pattern">The pattern template string.</param>
    public static KyrolusGatewayTransform PathPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.PathPattern] = pattern
        });
    }

    #endregion

    #region Factory Methods - Request Header Transforms

    /// <summary>
    /// Sets or appends a request header forwarded to the upstream backend.
    /// </summary>
    /// <param name="headerName">The HTTP header name.</param>
    /// <param name="value">The value to set or append.</param>
    /// <param name="action">The transform action: <c>"Set"</c> (default) or <c>"Append"</c>.</param>
    public static KyrolusGatewayTransform RequestHeader(string headerName, string value, string action = KyrolusGatewayTransformNames.SetAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentNullException.ThrowIfNull(value);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.RequestHeader] = headerName,
            [action] = value
        });
    }

    /// <summary>
    /// Sets or appends a request header forwarded to the upstream backend using strongly-typed <see cref="KyrolusTransformAction"/>.
    /// </summary>
    public static KyrolusGatewayTransform RequestHeader(string headerName, string value, KyrolusTransformAction action)
        => RequestHeader(headerName, value, action == KyrolusTransformAction.Append ? KyrolusGatewayTransformNames.AppendAction : KyrolusGatewayTransformNames.SetAction);

    /// <summary>
    /// Strips the specified request header before forwarding to the backend.
    /// </summary>
    /// <param name="headerName">The HTTP header name to remove.</param>
    public static KyrolusGatewayTransform RequestHeaderRemove(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.RequestHeaderRemove] = headerName
        });
    }

    /// <summary>
    /// Restricts allowed request headers forwarded to the backend; all unlisted headers are stripped.
    /// </summary>
    /// <param name="allowedHeaders">The names of allowed request headers.</param>
    public static KyrolusGatewayTransform RequestHeadersAllowed(params string[] allowedHeaders)
    {
        ArgumentNullException.ThrowIfNull(allowedHeaders);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.RequestHeadersAllowed] = string.Join(";", allowedHeaders)
        });
    }

    /// <summary>
    /// Configures whether the original client <c>Host</c> header is preserved when proxying to the upstream backend.
    /// </summary>
    /// <param name="useOriginal">Whether to preserve the original client Host header.</param>
    public static KyrolusGatewayTransform RequestHeaderOriginalHost(bool useOriginal = true)
    {
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.RequestHeaderOriginalHost] = useOriginal ? "true" : "false"
        });
    }

    /// <summary>
    /// Forwards verified client certificate details in the specified HTTP header to the backend.
    /// </summary>
    /// <param name="headerName">The header name to store client certificate details.</param>
    public static KyrolusGatewayTransform ClientCert(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.ClientCert] = headerName
        });
    }

    #endregion

    #region Factory Methods - Response Header Transforms

    /// <summary>
    /// Adds or replaces an HTTP response header returned to the downstream client.
    /// </summary>
    /// <param name="headerName">The response header name.</param>
    /// <param name="value">The header value.</param>
    /// <param name="action">The transform action: <c>"Set"</c> (default) or <c>"Append"</c>.</param>
    /// <param name="when">When to apply the transform: <c>"Always"</c> (default) or <c>"Success"</c>.</param>
    public static KyrolusGatewayTransform ResponseHeader(string headerName, string value, string action = KyrolusGatewayTransformNames.SetAction, string when = KyrolusGatewayTransformNames.AlwaysCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentNullException.ThrowIfNull(value);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.ResponseHeader] = headerName,
            [action] = value,
            [KyrolusGatewayTransformNames.WhenKey] = when
        });
    }

    /// <summary>
    /// Adds or replaces an HTTP response header returned to the downstream client using strongly-typed enums.
    /// </summary>
    public static KyrolusGatewayTransform ResponseHeader(string headerName, string value, KyrolusTransformAction action, KyrolusTransformWhen when = KyrolusTransformWhen.Always)
        => ResponseHeader(
            headerName,
            value,
            action == KyrolusTransformAction.Append ? KyrolusGatewayTransformNames.AppendAction : KyrolusGatewayTransformNames.SetAction,
            when == KyrolusTransformWhen.Success ? KyrolusGatewayTransformNames.SuccessCondition : KyrolusGatewayTransformNames.AlwaysCondition);

    /// <summary>
    /// Adds or replaces an HTTP response header using the <c>ResponseHeaderValue</c> key format.
    /// </summary>
    public static KyrolusGatewayTransform ResponseHeaderValue(string headerName, string value, string action = KyrolusGatewayTransformNames.SetAction, string when = KyrolusGatewayTransformNames.AlwaysCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentNullException.ThrowIfNull(value);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.ResponseHeaderValue] = headerName,
            [action] = value,
            [KyrolusGatewayTransformNames.WhenKey] = when
        });
    }

    /// <summary>
    /// Strips the specified response header before returning the response to the client.
    /// </summary>
    /// <param name="headerName">The response header name to remove.</param>
    /// <param name="when">When to remove: <c>"Always"</c> (default) or <c>"Success"</c>.</param>
    public static KyrolusGatewayTransform ResponseHeaderRemove(string headerName, string when = KyrolusGatewayTransformNames.AlwaysCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.ResponseHeaderRemove] = headerName,
            [KyrolusGatewayTransformNames.WhenKey] = when
        });
    }

    /// <summary>
    /// Restricts allowed response headers returned to the client; all unlisted headers are stripped.
    /// </summary>
    public static KyrolusGatewayTransform ResponseHeadersAllowed(params string[] allowedHeaders)
    {
        ArgumentNullException.ThrowIfNull(allowedHeaders);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.ResponseHeadersAllowed] = string.Join(";", allowedHeaders)
        });
    }

    /// <summary>
    /// Restricts allowed response trailers returned to the client.
    /// </summary>
    public static KyrolusGatewayTransform ResponseTrailersAllowed(params string[] allowedTrailers)
    {
        ArgumentNullException.ThrowIfNull(allowedTrailers);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.ResponseTrailersAllowed] = string.Join(";", allowedTrailers)
        });
    }

    #endregion

    #region Factory Methods - Query Parameter Transforms

    /// <summary>
    /// Sets or appends a static query string parameter on proxied requests sent to the backend.
    /// </summary>
    /// <param name="queryKey">The query parameter name.</param>
    /// <param name="value">The query parameter value.</param>
    /// <param name="action">The transform action: <c>"Set"</c> (default) or <c>"Append"</c>.</param>
    public static KyrolusGatewayTransform QueryValueParameter(string queryKey, string value, string action = KyrolusGatewayTransformNames.SetAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryKey);
        ArgumentNullException.ThrowIfNull(value);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.QueryValueParameter] = queryKey,
            [action] = value
        });
    }

    /// <summary>
    /// Sets or appends a static query string parameter using strongly-typed <see cref="KyrolusTransformAction"/>.
    /// </summary>
    public static KyrolusGatewayTransform QueryValueParameter(string queryKey, string value, KyrolusTransformAction action)
        => QueryValueParameter(queryKey, value, action == KyrolusTransformAction.Append ? KyrolusGatewayTransformNames.AppendAction : KyrolusGatewayTransformNames.SetAction);

    /// <summary>
    /// Sets or appends a query parameter using a matched route template value.
    /// </summary>
    /// <param name="queryKey">The target query parameter name.</param>
    /// <param name="routeKey">The source route parameter key.</param>
    /// <param name="action">The transform action: <c>"Set"</c> (default) or <c>"Append"</c>.</param>
    public static KyrolusGatewayTransform QueryRouteParameter(string queryKey, string routeKey, string action = KyrolusGatewayTransformNames.SetAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.QueryRouteParameter] = queryKey,
            [action] = routeKey
        });
    }

    /// <summary>
    /// Sets or appends a query parameter using a matched route template value with strongly-typed <see cref="KyrolusTransformAction"/>.
    /// </summary>
    public static KyrolusGatewayTransform QueryRouteParameter(string queryKey, string routeKey, KyrolusTransformAction action)
        => QueryRouteParameter(queryKey, routeKey, action == KyrolusTransformAction.Append ? KyrolusGatewayTransformNames.AppendAction : KyrolusGatewayTransformNames.SetAction);

    /// <summary>
    /// Strips the specified query parameter from the request before proxying to the backend.
    /// </summary>
    /// <param name="queryKey">The query parameter name to remove.</param>
    public static KyrolusGatewayTransform QueryRemoveParameter(string queryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryKey);
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.QueryRemoveParameter] = queryKey
        });
    }

    #endregion

    #region Factory Methods - Forwarded Transforms

    /// <summary>
    /// Configures the RFC 7239 <c>Forwarded</c> request header.
    /// </summary>
    public static KyrolusGatewayTransform Forwarded(string forwarded = "proto,host,for", string forFormat = "Random", string prefix = "Forwarded")
    {
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.Forwarded] = forwarded,
            ["ForFormat"] = forFormat,
            ["Prefix"] = prefix
        });
    }

    /// <summary>
    /// Configures the non-standard <c>X-Forwarded-*</c> headers (<c>X-Forwarded-For</c>, <c>X-Forwarded-Proto</c>, <c>X-Forwarded-Host</c>, <c>X-Forwarded-Prefix</c>).
    /// </summary>
    public static KyrolusGatewayTransform XForwarded(string action = KyrolusGatewayTransformNames.SetAction, string forFormat = "Random")
    {
        return new(new Dictionary<string, string>
        {
            [KyrolusGatewayTransformNames.XForwarded] = action,
            ["ForFormat"] = forFormat
        });
    }

    #endregion

    #region Factory Methods - Custom & Parsing

    /// <summary>
    /// Creates a custom transform from an arbitrary dictionary of key-value settings.
    /// </summary>
    /// <param name="values">The custom transform dictionary.</param>
    public static KyrolusGatewayTransform Custom(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(values);
    }

    /// <summary>
    /// Creates a custom transform from a single key-value pair.
    /// </summary>
    public static KyrolusGatewayTransform Custom(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new(new Dictionary<string, string> { [key] = value ?? string.Empty });
    }

    /// <summary>
    /// Resolves a <see cref="KyrolusGatewayTransform"/> from an existing key-value dictionary.
    /// </summary>
    public static KyrolusGatewayTransform From(IReadOnlyDictionary<string, string> values)
    {
        return new(values ?? EmptyDict);
    }

    #endregion

    #region IReadOnlyDictionary<string, string> Implementation

    /// <inheritdoc />
    public string this[string key] => Values[key];

    /// <inheritdoc />
    public IEnumerable<string> Keys => Values.Keys;

    /// <inheritdoc />
    IEnumerable<string> IReadOnlyDictionary<string, string>.Values => Values.Values;

    /// <inheritdoc />
    public int Count => Values.Count;

    /// <inheritdoc />
    public bool ContainsKey(string key) => Values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out string value) => Values.TryGetValue(key, out value!);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Values.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => Values.GetEnumerator();

    #endregion

    #region Equality and Operators

    /// <inheritdoc />
    public bool Equals(KyrolusGatewayTransform other)
    {
        if (ReferenceEquals(_values, other._values)) return true;
        if (Count != other.Count) return false;
        foreach (var kvp in this)
        {
            if (!other.TryGetValue(kvp.Key, out var otherVal) || !string.Equals(kvp.Value, otherVal, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Count);
        foreach (var kvp in Values)
        {
            hash.Add(kvp.Key, StringComparer.OrdinalIgnoreCase);
            hash.Add(kvp.Value, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Implicitly converts a <see cref="KyrolusGatewayTransform"/> to a new <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    public static implicit operator Dictionary<string, string>(KyrolusGatewayTransform transform) => new(transform.Values);

    /// <summary>
    /// Implicitly converts a dictionary to a <see cref="KyrolusGatewayTransform"/>.
    /// </summary>
    public static implicit operator KyrolusGatewayTransform(Dictionary<string, string> dict) => From(dict);

    #endregion
}
