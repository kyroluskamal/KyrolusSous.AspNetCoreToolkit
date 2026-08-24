namespace KyrolusSous.Compression;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="KyrolusResponseCompressionMiddleware"/> to the application request pipeline.
    /// Automatically compresses HTTP response bodies based on configured MIME types, excluded paths, and client Accept-Encoding.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseKyrolusResponseCompression(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<KyrolusResponseCompressionMiddleware>();
    }
}
