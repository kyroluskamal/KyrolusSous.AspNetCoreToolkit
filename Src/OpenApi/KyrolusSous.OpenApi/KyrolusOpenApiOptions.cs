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

    public List<ApiVersionInfo> ApiVersions { get; set; } = [];

    public List<OpenApiServerInfo> Servers { get; set; } = [];

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
}

public class ApiVersionInfo
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

public class OpenApiServerInfo(string url, string? description = null)
{
    public string Url { get; set; } = url;

    public string? Description { get; set; } = description;
}
