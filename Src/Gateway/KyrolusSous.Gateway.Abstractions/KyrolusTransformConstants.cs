namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Defines the operation action applied by a header or query transform.
/// </summary>
public enum KyrolusTransformAction
{
    /// <summary>
    /// Replaces the existing value or sets it if not present.
    /// </summary>
    Set,

    /// <summary>
    /// Appends the value to any existing values, comma-separated.
    /// </summary>
    Append
}

/// <summary>
/// Defines execution conditions determining when a response transform is applied.
/// </summary>
public enum KyrolusTransformWhen
{
    /// <summary>
    /// The transform is always applied, regardless of the response status code.
    /// </summary>
    Always,

    /// <summary>
    /// The transform is only applied if the response status code is less than 400 (successful).
    /// </summary>
    Success
}

/// <summary>
/// Standard transform key and action names for gateway routing transforms.
/// </summary>
public static class KyrolusGatewayTransformNames
{
    // Path Transforms
    public const string PathPrefix = "PathPrefix";
    public const string PathRemovePrefix = "PathRemovePrefix";
    public const string PathSet = "PathSet";
    public const string PathPattern = "PathPattern";

    // Request Header Transforms
    public const string RequestHeader = "RequestHeader";
    public const string RequestHeaderValue = "RequestHeaderValue";
    public const string RequestHeaderRemove = "RequestHeaderRemove";
    public const string RequestHeadersAllowed = "RequestHeadersAllowed";
    public const string RequestHeaderOriginalHost = "RequestHeaderOriginalHost";
    public const string ClientCert = "ClientCert";

    // Response Header Transforms
    public const string ResponseHeader = "ResponseHeader";
    public const string ResponseHeaderValue = "ResponseHeaderValue";
    public const string ResponseHeaderRemove = "ResponseHeaderRemove";
    public const string ResponseHeadersAllowed = "ResponseHeadersAllowed";
    public const string ResponseTrailersAllowed = "ResponseTrailersAllowed";

    // Query Transforms
    public const string QueryValueParameter = "QueryValueParameter";
    public const string QueryRouteParameter = "QueryRouteParameter";
    public const string QueryRemoveParameter = "QueryRemoveParameter";

    // Forwarded Transforms
    public const string XForwarded = "X-Forwarded";
    public const string Forwarded = "Forwarded";

    // Actions & When
    public const string SetAction = "Set";
    public const string AppendAction = "Append";
    public const string RemoveAction = "Remove";
    public const string OffAction = "Off";
    public const string WhenKey = "When";
    public const string AlwaysCondition = "Always";
    public const string SuccessCondition = "Success";
}
