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
    private readonly KyrolusExceptionTranslator translator = dependencies.Translator;
    private readonly IKyrolusErrorResponseWriter responseWriter = dependencies.ResponseWriter;
    private readonly KyrolusHttpErrorContextFactory contextFactory = dependencies.ContextFactory;
    private readonly KyrolusExceptionHandlingOptions options = dependencies.Options;
    private readonly ILogger logger = dependencies.Logger;

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
            var errorContext = contextFactory.Create(httpContext);
            var mapping = translator.TranslateToMapping(ex, errorContext);

            var isIgnoredLogType = options.IgnoredExceptionLogTypes.Count > 0 &&
                                    options.IgnoredExceptionLogTypes.Any(t => t.IsInstanceOfType(ex));

            if (mapping.ShouldLog && options.LogUnhandledExceptions && !isIgnoredLogType)
                LogException(mapping, ex, errorContext);

            if (httpContext.Response.HasStarted)
            {
                logger.LogWarning("The response has already started, the exception handling middleware cannot write the error response.");
                throw;
            }

            await responseWriter.WriteAsync(httpContext, mapping, errorContext, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }

    private void LogException(KyrolusExceptionMapping mapping, Exception exception, KyrolusErrorContext context)
    {
        var logLevel = options.LogLevelSelector(mapping, exception);

        if (!logger.IsEnabled(logLevel)) return;

        logger.Log(
            logLevel,
            exception,
            "Unhandled exception mapped to {ErrorCode} ({StatusCode}). TraceId={TraceId}, CorrelationId={CorrelationId}, UserId={UserId}, TenantId={TenantId}, Path={Path}, Method={Method}",
            mapping.Error.Code,
            (int)mapping.StatusCode,
            context.TraceId,
            context.CorrelationId,
            context.UserId,
            context.TenantId,
            context.Path,
            context.Method);
    }
}
