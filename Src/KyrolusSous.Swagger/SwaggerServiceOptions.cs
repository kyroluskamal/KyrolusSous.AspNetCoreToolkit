// Src/KyrolusSous.Swagger/SwaggerServiceOptions.cs
using System.Reflection;

namespace KyrolusSous.Swagger;

/// <summary>
/// Options for configuring Swagger/OpenAPI documentation and UI.
/// </summary>
public class SwaggerServiceOptions
{
    /// <summary>
    /// Defines information for multiple API versions. At least one version must be provided.
    /// </summary>
    public List<ApiVersionInfo> ApiVersions { get; set; } = [new()];

    // XML Comments Options
    /// <summary>
    /// Enables Swagger to include XML comments from documentation files.
    /// Default is false.
    /// </summary>
    public bool EnableXmlComments { get; set; } = false;

    /// <summary>
    /// A list of assemblies from which to load XML documentation files.
    /// If empty and EnableXmlComments is true, it defaults to the entry assembly.
    /// </summary>
    public List<Assembly> XmlCommentAssemblies { get; set; } = new List<Assembly>();

    // Security (Authentication) Options
    /// <summary>
    /// Enables JWT Bearer token authentication in Swagger UI.
    /// Default is true.
    /// </summary>
    public bool EnableJwtBearerAuth { get; set; } = true;

    /// <summary>
    /// Description for the JWT Bearer authentication scheme in Swagger UI.
    /// </summary>
    public string JwtBearerDescription { get; set; } = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"";

    /// <summary>
    /// The scheme name for JWT Bearer authentication. Default is "Bearer".
    /// </summary>
    public string JwtBearerScheme { get; set; } = "Bearer";

    /// <summary>
    /// Enables OAuth2 authentication in Swagger UI.
    /// Default is false.
    /// </summary>
    public bool EnableOAuth2Auth { get; set; } = false;

    /// <summary>
    /// The scheme name for OAuth2 authentication. Default is "OAuth2".
    /// </summary>
    public string OAuth2SchemeName { get; set; } = "OAuth2";

    /// <summary>
    /// Description for the OAuth2 authentication scheme in Swagger UI.
    /// </summary>
    public string OAuth2Description { get; set; } = "OAuth2 Authorization Code flow for client authentication.";

    /// <summary>
    /// The URL for OAuth2 authorization endpoint. Required for "authorizationCode" and "implicit" flows.
    /// </summary>
    public string OAuth2AuthorizationUrl { get; set; } = "";

    /// <summary>
    /// The URL for OAuth2 token endpoint. Required for "authorizationCode", "password", and "clientCredentials" flows.
    /// </summary>
    public string OAuth2TokenUrl { get; set; } = "";

    /// <summary>
    /// A dictionary of OAuth2 scopes where key is the scope name and value is the description.
    /// </summary>
    public IDictionary<string, string> OAuth2Scopes { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// The OAuth2 flow type. Supported values: "authorizationCode", "implicit", "password", "clientCredentials".
    /// </summary>
    public string? OAuth2Flow { get; set; }

    // Swashbuckle Specific Options
    /// <summary>
    /// Enables processing of Swagger annotations ([SwaggerOperation], [SwaggerResponse], etc.).
    /// Requires Swashbuckle.AspNetCore.Annotations NuGet package.
    /// Default is false.
    /// </summary>
    public bool EnableAnnotations { get; set; } = false;

    /// <summary>
    /// Enables Swagger to correctly reflect nullable reference types in schema.
    /// Default is true. This often requires the API project to have
    /// &lt;Nullable&gt;enable&lt;/Nullable&gt; in its .csproj.
    /// </summary>
    public bool EnableNullableReferenceTypesSupport { get; set; } = true;

    /// <summary>
    /// If true, Swagger will treat non-nullable reference types as required (non-nullable) in the schema.
    /// This property works in conjunction with EnableNullableReferenceTypesSupport.
    /// Default is true.
    /// </summary>
    public bool SupportNonNullableReferenceTypes { get; set; } = true;

    // Swagger UI Options (Defaults applied by UseConfiguredSwaggerUI)
    /// <summary>
    /// The route prefix for Swagger UI. Default is "swagger".
    /// </summary>
    public string? UiRoutePrefix { get; set; } = "swagger";

    /// <summary>
    /// The title of the HTML document for Swagger UI.
    /// If null, it defaults to the title of the first API version.
    /// </summary>
    public string? UiDocumentTitle { get; set; }

    /// <summary>
    /// Displays the request duration in Swagger UI.
    /// Default is false.
    /// </summary>
    public bool UiDisplayRequestDuration { get; set; } = false;

    /// <summary>
    /// Enables deep linking for tags and operations in Swagger UI.
    /// Default is true.
    /// </summary>
    public bool UiEnableDeepLinking { get; set; } = true;

    /// <summary>
    /// Enables the filter box in Swagger UI.
    /// Default is false.
    /// </summary>
    public bool UiEnableFilter { get; set; } = false;

    /// <summary>
    /// Shows vendor extensions in Swagger UI.
    /// Default is false.
    /// </summary>
    public bool UiShowExtensions { get; set; } = false;

    /// <summary>
    /// Shows common extensions (x- schemas) in Swagger UI.
    /// Default is false.
    /// </summary>
    public bool UiShowCommonExtensions { get; set; } = false;

    /// <summary>
    /// Enables persistence of authorization data across browser sessions in Swagger UI.
    /// Default is false.
    /// </summary>
    public bool UiEnablePersistAuthorization { get; set; } = false;

    /// <summary>
    /// Displays the operationId in Swagger UI.
    /// Default is false.
    /// </summary>
    public bool UiDisplayOperationId { get; set; } = false;

    /// <summary>
    /// A list of HTTP methods that will display the "Try it out" button in Swagger UI.
    /// Default includes "get", "post", "put", "delete", "patch".
    /// </summary>
    public List<string> UiSupportedSubmitMethods { get; set; } = ["get", "post", "put", "delete", "patch"];
    /// <summary>
    /// A list of types that implement ISchemaFilter to apply custom schema modifications.
    /// </summary>
    public List<Type> SchemaFilters { get; set; } = [];

    /// <summary>
    /// A list of types that implement IOperationFilter to apply custom operation modifications.
    /// </summary>
    public List<Type> OperationFilters { get; set; } = [];

    /// <summary>
    /// A list of types that implement IDocumentFilter to apply custom document modifications.
    /// </summary>
    public List<Type> DocumentFilters { get; set; } = [];
}

/// <summary>
/// Represents information for a specific API version document.
/// </summary>
public class ApiVersionInfo
{
    /// <summary>
    /// The version string (e.g., "v1", "2.0").
    /// Default is "v1".
    /// </summary>
    public string Version { get; set; } = "v1";

    /// <summary>
    /// The title of this API version.
    /// Default is "API Documentation".
    /// </summary>
    public string Title { get; set; } = "API Documentation";

    /// <summary>
    /// A general description of this API version.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// A URL to the terms of service for this API version.
    /// </summary>
    public string? TermsOfServiceUrl { get; set; }

    // Contact Information specific to this version
    /// <summary>
    /// The name of the contact person/organization for this API version.
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>
    /// The email address of the contact person for this API version.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// A URL for the contact person/organization for this API version.
    /// </summary>
    public string? ContactUrl { get; set; }

    // License Information specific to this version
    /// <summary>
    /// The name of the license for this API version (e.g., "MIT License").
    /// </summary>
    public string? LicenseName { get; set; }

    /// <summary>
    /// A URL to the license for this API version.
    /// </summary>
    public string? LicenseUrl { get; set; }


}