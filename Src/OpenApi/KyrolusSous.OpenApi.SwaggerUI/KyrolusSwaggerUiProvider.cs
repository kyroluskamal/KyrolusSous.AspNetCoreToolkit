namespace KyrolusSous.OpenApi.SwaggerUI;

/// <summary>
/// Documentation UI provider that maps Swagger UI via Swashbuckle.
/// </summary>
public sealed class KyrolusSwaggerUiProvider : IKyrolusOpenApiUiProvider
{
    private readonly Action<SwaggerUIOptions>? _configureSwaggerUi;

    public KyrolusSwaggerUiProvider() : this(null)
    {
    }

    public KyrolusSwaggerUiProvider(Action<SwaggerUIOptions>? configureSwaggerUi)
    {
        _configureSwaggerUi = configureSwaggerUi;
    }

    /// <inheritdoc />
    public string ProviderName => "SwaggerUI";

    /// <inheritdoc />
    public void MapUi(WebApplication app, KyrolusOpenApiOptions options, IReadOnlyList<ApiVersionInfo> versions)
    {
        if (!options.EnableSwaggerUi || versions.Count == 0)
        {
            return;
        }

        app.UseSwaggerUI(swaggerUiOptions =>
        {
            swaggerUiOptions.RoutePrefix = options.SwaggerUiRoutePrefix;
            swaggerUiOptions.DocumentTitle = options.UiDocumentTitle ?? versions[0].Title;

            foreach (var version in versions)
            {
                swaggerUiOptions.SwaggerEndpoint($"/openapi/{version.Version}.json", $"{version.Title} {version.Version}");
            }

            _configureSwaggerUi?.Invoke(swaggerUiOptions);
        });
    }
}
