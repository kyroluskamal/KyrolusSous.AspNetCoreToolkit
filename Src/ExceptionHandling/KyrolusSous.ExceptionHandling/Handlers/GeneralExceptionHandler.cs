using System.Net;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.ExceptionHandling.Handlers;

public class GeneralExceptionHandler(ILogger<GeneralExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
            logger, httpContext, HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest,
            "Bad request", exception.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }
}
