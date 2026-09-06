using Microsoft.AspNetCore.Builder;

namespace KyrolusSous.Auth.TokenRevocation;

/// <summary>
/// Extension methods for attaching Kyrolus token revocation middleware to the HTTP request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds token revocation middleware to the pipeline, terminating revoked access tokens with HTTP 401.
    /// Should be placed after <c>app.UseAuthentication()</c> and before endpoints or reverse proxy routing.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseKyrolusTokenRevocation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<KyrolusTokenRevocationMiddleware>();
    }
}
