namespace KyrolusSous.ExceptionHandling.Runtime;

/// <summary>
/// MVC and API Controller action filter that handles unhandled action exceptions before they leave the MVC pipeline.
/// </summary>
/// <remarks>
/// Use when running in standard MVC / Web API controllers where controller-level filter execution is preferred.
/// </remarks>
public sealed class KyrolusExceptionFilter(KyrolusExceptionHandlingDependencies dependencies) : IAsyncExceptionFilter
{
    private readonly KyrolusExceptionHandlingDependencies dependencies = dependencies;

    /// <summary>
    /// Executes when an unhandled exception occurs inside a controller action.
    /// </summary>
    /// <param name="context">The MVC exception context.</param>
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.ExceptionHandled) return;

        var errorContext = dependencies.ContextFactory.Create(context.HttpContext);
        var mapping = dependencies.TranslateAndLog(context.Exception, errorContext);

        context.ExceptionHandled = true;
        context.HttpContext.Response.Clear();
        await dependencies.ResponseWriter.WriteAsync(context.HttpContext, mapping, errorContext, context.HttpContext.RequestAborted).ConfigureAwait(false);
        context.Result = new EmptyResult();
    }
}
