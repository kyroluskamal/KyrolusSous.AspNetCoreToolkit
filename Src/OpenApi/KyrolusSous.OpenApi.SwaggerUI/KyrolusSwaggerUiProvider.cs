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

        var routePrefix = string.IsNullOrWhiteSpace(options.SwaggerUiRoutePrefix)
            ? "swagger"
            : options.SwaggerUiRoutePrefix.Trim('/');

        app.UseSwaggerUI(swaggerUiOptions =>
        {
            swaggerUiOptions.RoutePrefix = routePrefix;
            swaggerUiOptions.DocumentTitle = options.UiDocumentTitle ?? versions[0].Title;

            foreach (var version in versions)
            {
                var endpointName = version.Title.Contains(version.Version, StringComparison.OrdinalIgnoreCase)
                    ? version.Title
                    : $"{version.Title} {version.Version}";

                swaggerUiOptions.SwaggerEndpoint($"/openapi/{version.Version}.json", endpointName);
            }

            if (!string.IsNullOrWhiteSpace(options.CustomCss))
            {
                swaggerUiOptions.InjectStylesheet(options.CustomCss);
            }

            if (!string.IsNullOrWhiteSpace(options.FaviconUrl))
            {
                swaggerUiOptions.HeadContent += $"<link rel=\"icon\" href=\"{options.FaviconUrl}\" />";
            }

            _configureSwaggerUi?.Invoke(swaggerUiOptions);
        });
    }
}
