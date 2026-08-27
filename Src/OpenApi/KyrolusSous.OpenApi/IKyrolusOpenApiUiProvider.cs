namespace KyrolusSous.OpenApi;

/// <summary>
/// Defines an extensible documentation UI provider (such as Swagger UI) that integrates
/// with <see cref="OpenApiServiceExtensions.MapKyrolusOpenApi"/>.
/// </summary>
public interface IKyrolusOpenApiUiProvider
{
    /// <summary>
    /// Gets the unique identifier or name of the UI provider (e.g. "SwaggerUI").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Maps the documentation UI endpoints to the application.
    /// </summary>
    void MapUi(WebApplication app, KyrolusOpenApiOptions options, IReadOnlyList<ApiVersionInfo> versions);
}
