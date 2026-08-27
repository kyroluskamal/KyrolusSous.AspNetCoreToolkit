using KyrolusSous.OpenApi.SwaggerUI;

[assembly: HostingStartup(typeof(KyrolusSwaggerUiHostingStartup))]

namespace KyrolusSous.OpenApi.SwaggerUI;

/// <summary>
/// Hosting startup that automatically registers Swagger UI provider in dependency injection
/// when this package is referenced.
/// </summary>
public sealed class KyrolusSwaggerUiHostingStartup : IHostingStartup
{
    /// <inheritdoc />
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IKyrolusOpenApiUiProvider, KyrolusSwaggerUiProvider>();
        });
    }
}
