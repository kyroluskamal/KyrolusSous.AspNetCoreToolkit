namespace KyrolusSous.OpenApi;

public class KyrolusOpenApiOptions
{
    public string? Title { get; set; }

    public string? Version { get; set; } = "v1";

    public string? Description { get; set; }

    public string? TermsOfServiceUrl { get; set; }

    public string? ContactName { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactUrl { get; set; }

    public string? LicenseName { get; set; }

    public string? LicenseUrl { get; set; }

    public List<KyrolusApiVersionInfo> ApiVersions { get; set; } = [];

    public List<KyrolusOpenApiServerInfo> Servers { get; set; } = [];

    public bool EnableApiVersioning { get; set; } = false;

    public bool EnableSmartAutoTagging { get; set; } = true;

    public bool EnableStandardErrorResponses { get; set; } = true;

    public bool EnableCorrelationIdHeader { get; set; } = false;

    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";

    public bool EnableJwtBearerAuth { get; set; } = true;

    public string JwtBearerDescription { get; set; } = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"";

    public string JwtBearerScheme { get; set; } = "Bearer";

    public bool EnableApiKeyAuth { get; set; } = false;

    public string ApiKeySchemeName { get; set; } = "ApiKey";

    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

    public string ApiKeyDescription { get; set; } = "API Key authentication header using the X-Api-Key scheme.";

    public bool EnableBasicAuth { get; set; } = false;

    public string BasicAuthSchemeName { get; set; } = "Basic";

    public string BasicAuthDescription { get; set; } = "HTTP Basic Authentication header.";

    public bool EnableOAuth2Auth { get; set; } = false;

    public string OAuth2SchemeName { get; set; } = "OAuth2";

    public string OAuth2Description { get; set; } = "OAuth2 Authorization Code flow for client authentication.";

    public string OAuth2AuthorizationUrl { get; set; } = "";

    public string OAuth2TokenUrl { get; set; } = "";

    public IDictionary<string, string> OAuth2Scopes { get; set; } = new Dictionary<string, string>();

    public string? OAuth2Flow { get; set; }

    public bool EnableXmlComments { get; set; } = true;

    public List<Assembly> XmlCommentAssemblies { get; set; } = [];

    public List<string> XmlDocAbsolutePaths { get; set; } = [];

    public bool EnableInNonDevelopmentEnvironments { get; set; } = false;

    public bool EnableScalarUi { get; set; } = true;

    public string ScalarRoutePrefix { get; set; } = "scalar";

    public ScalarTheme ScalarTheme { get; set; } = ScalarTheme.Default;

    public string ScalarSearchHotKey { get; set; } = "k";

    public bool EnableSwaggerUi { get; set; } = true;

    public string SwaggerUiRoutePrefix { get; set; } = "swagger";

    public bool EnableReDocUi { get; set; } = true;

    public string ReDocRoutePrefix { get; set; } = "redoc";

    public string? CustomCss { get; set; }

    public string? FaviconUrl { get; set; }

    public string? UiDocumentTitle { get; set; }

    /// <summary>
    /// Gets or sets whether smart endpoint authorization detection is enabled.
    /// When enabled, endpoints marked with [AllowAnonymous] do not require authorization,
    /// while endpoints with [Authorize] have security requirements and documented roles/permissions.
    /// Default is true.
    /// </summary>
    public bool EnableSmartAuthorization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether endpoints require authorization by default unless marked with [AllowAnonymous].
    /// Default is false.
    /// </summary>
    public bool RequireAuthorizationByDefault { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to add the multi-tenant header parameter to operations.
    /// Default is false.
    /// </summary>
    public bool EnableTenantIdHeader { get; set; } = false;

    /// <summary>
    /// Gets or sets the name of the multi-tenant header. Default is "X-Tenant-Id".
    /// </summary>
    public string TenantIdHeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// Gets or sets the description for the multi-tenant header.
    /// </summary>
    public string TenantIdDescription { get; set; } = "Tenant identifier for multi-tenant requests.";

    /// <summary>
    /// Gets or sets whether standard error responses should include an RFC 7807 ProblemDetails schema reference.
    /// Default is true.
    /// </summary>
    public bool IncludeProblemDetailsSchema { get; set; } = true;

    /// <summary>
    /// Gets or sets whether 404 Not Found standard response is included. Default is false.
    /// </summary>
    public bool IncludeNotFoundResponse { get; set; } = false;

    /// <summary>
    /// Gets or sets whether 422 Unprocessable Entity standard response is included. Default is false.
    /// </summary>
    public bool IncludeUnprocessableEntityResponse { get; set; } = false;

    /// <summary>
    /// Gets or sets whether tags in the OpenAPI document should be sorted alphabetically. Default is true.
    /// </summary>
    public bool SortTagsAlphabetically { get; set; } = true;

    /// <summary>
    /// Gets or sets whether endpoints decorated with [Obsolete] are automatically marked as deprecated.
    /// Default is true.
    /// </summary>
    public bool EnableDeprecationTransformer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether endpoints with rate limiting metadata automatically document HTTP 429 response.
    /// Default is true.
    /// </summary>
    public bool EnableRateLimitingTransformer { get; set; } = true;

    /// <summary>
    /// Optional delegate to configure the underlying Microsoft.AspNetCore.OpenApi.OpenApiOptions directly.
    /// </summary>
    public Action<OpenApiOptions>? ConfigureOpenApiOptions { get; set; }

    /// <summary>
    /// Configures OAuth2 security scheme for integration with KyrolusSous.Auth.OpenIddict.
    /// </summary>
    public KyrolusOpenApiOptions ConfigureForOpenIddict(
        string? authorizationUrl = "/connect/authorize",
        string? tokenUrl = "/connect/token",
        IDictionary<string, string>? scopes = null)
    {
        EnableOAuth2Auth = true;
        OAuth2SchemeName = "OpenIddict";
        OAuth2Flow = "authorizationcode";
        OAuth2AuthorizationUrl = authorizationUrl ?? "/connect/authorize";
        OAuth2TokenUrl = tokenUrl ?? "/connect/token";
        OAuth2Description = "OpenIddict OAuth 2.0 / OpenID Connect authorization.";

        scopes ??= new Dictionary<string, string>
        {
            ["openid"] = "Standard OpenID Connect scope",
            ["profile"] = "User profile claims",
            ["email"] = "User email address",
            ["offline_access"] = "Refresh token access"
        };

        OAuth2Scopes = scopes;
        return this;
    }
}

public class KyrolusApiVersionInfo
{
    public string Version { get; set; } = "v1";

    public string Title { get; set; } = "API Documentation";

    public string? Description { get; set; }

    public string? TermsOfServiceUrl { get; set; }

    public string? ContactName { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactUrl { get; set; }

    public string? LicenseName { get; set; }

    public string? LicenseUrl { get; set; }
}

public class KyrolusOpenApiServerInfo(string url, string? description = null)
{
    public string Url { get; set; } = url;

    public string? Description { get; set; } = description;
}


