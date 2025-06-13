
namespace KyrolusSous.ExceptionHandling;

public class ExceptionHandlingMiddleware(RequestDelegate next, IEnumerable<IExceptionHandler> exceptionHandlers)
{
    private readonly RequestDelegate _next = next;
    private readonly IEnumerable<IExceptionHandler> _exceptionHandlers = exceptionHandlers;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {

            // Iterate through all exception handlers
            foreach (var handler in _exceptionHandlers)
            {
                var handled = await handler.TryHandleAsync(context, ex, context.RequestAborted);
                if (handled)
                {
                    // The exception was handled; stop further processing
                    return;
                }
            }

            // If no handler could handle the exception, fallback to default/general handler
            var generalHandler = _exceptionHandlers.OfType<GeneralExceptionHandler>().FirstOrDefault();
            if (generalHandler != null)
            {
                await generalHandler.TryHandleAsync(context, ex, context.RequestAborted);
            }

            // var handler = _exceptionHandlers.FirstOrDefault(h => h.GetType().IsAssignableFrom(ex.GetType()));
            // if (handler != null)
            // {
            //     await handler.TryHandleAsync(context, ex, context.RequestAborted);
            // }
            // else
            // {
            //     var generalHandler = _exceptionHandlers.OfType<GeneralExceptionHandler>().FirstOrDefault();
            //     if (generalHandler != null)
            //     {
            //         await generalHandler.TryHandleAsync(context, ex, context.RequestAborted);
            //     }
            // }
        }
    }
}


