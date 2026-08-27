using Microsoft.AspNetCore.Authentication;

namespace KyrolusSous.Auth.ApiKey;

/// <summary>
/// Options for configuring API key authentication.
/// </summary>
public sealed class KyrolusApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";

    /// <summary>
    /// Gets or sets the HTTP request header name used to pass the API key. Defaults to <c>X-API-Key</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Gets or sets whether to accept the API key from a query string parameter. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowQueryParameter { get; set; }

    /// <summary>
    /// Gets or sets the query parameter name when <see cref="AllowQueryParameter"/> is enabled. Defaults to <c>api_key</c>.
    /// </summary>
    public string QueryParameterName { get; set; } = "api_key";
}
