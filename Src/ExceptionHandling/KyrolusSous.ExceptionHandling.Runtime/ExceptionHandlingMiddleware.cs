namespace KyrolusSous.ExceptionHandling.Runtime;

/// <summary>
/// ASP.NET Core middleware that catches all unhandled exceptions during request execution,
/// translating them into RFC 7807 problem responses with diagnostic tracing.
/// </summary>
/// <remarks>
/// Acts as the central safety net for HTTP requests. It captures ambient context (trace ID, correlation ID, user ID, tenant ID),
/// evaluates registered exception mappers, applies logging policies, and writes a clean JSON error envelope.
/// </remarks>
/// <example>
/// <code>
/// // Program.cs:
/// app.UseKyrolusExceptionHandling();
/// </code>
/// </example>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    KyrolusExceptionHandlingDependencies dependencies)
{
    private readonly RequestDelegate next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly KyrolusExceptionHandlingDependencies dependencies = dependencies;

    /// <summary>
    /// Invokes the middleware to execute the request pipeline with global exception trapping.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            await next(httpContext).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var errorContext = dependencies.ContextFactory.Create(httpContext);
            var mapping = dependencies.TranslateAndLog(ex, errorContext);

            if (httpContext.Response.HasStarted)
            {
                dependencies.Logger.LogWarning("The response has already started, the exception handling middleware cannot write the error response.");
                throw;
            }

            await dependencies.ResponseWriter.WriteAsync(httpContext, mapping, errorContext, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}
