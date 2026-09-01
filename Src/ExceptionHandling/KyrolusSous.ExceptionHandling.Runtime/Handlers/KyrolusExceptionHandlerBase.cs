namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

/// <summary>
/// Base class for ASP.NET Core native <see cref="IExceptionHandler"/> implementations. Delegates classification,
/// sanitization, localization, and logging entirely to <see cref="KyrolusExceptionHandlingDependencies"/> - the
/// same pipeline used by <see cref="ExceptionHandlingMiddleware"/> and <see cref="KyrolusExceptionFilter"/> - so
/// all three surfaces behave identically for the same exception instead of maintaining separate logic.
/// </summary>
/// <typeparam name="TException">The specific exception type handled.</typeparam>
public abstract class KyrolusExceptionHandlerBase<TException>(KyrolusExceptionHandlingDependencies dependencies) : IExceptionHandler
    where TException : Exception
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TException) return false;

        var errorContext = dependencies.ContextFactory.Create(httpContext);
        var mapping = dependencies.TranslateAndLog(exception, errorContext);

        if (!httpContext.Response.HasStarted)
            await dependencies.ResponseWriter.WriteAsync(httpContext, mapping, errorContext, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
