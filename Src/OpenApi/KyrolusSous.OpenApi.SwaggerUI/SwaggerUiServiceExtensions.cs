namespace KyrolusSous.OpenApi.SwaggerUI;

/// <summary>
/// Extension methods for explicitly registering and configuring Kyrolus Swagger UI.
/// </summary>
public static class SwaggerUiServiceExtensions
{
    /// <summary>
    /// Explicitly registers Kyrolus Swagger UI provider with optional configuration.
    /// </summary>
    public static IServiceCollection AddKyrolusSwaggerUi(
        this IServiceCollection services,
        Action<SwaggerUIOptions>? configure = null)
    {
        services.AddSingleton<IKyrolusOpenApiUiProvider>(new KyrolusSwaggerUiProvider(configure));
        return services;
    }
}
