using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.ExceptionHandling.Handlers;

public class SocketExceptionHandler(ILogger<SocketExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is SocketException socketException)
        {
            await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
                logger, httpContext, HttpStatusCode.InternalServerError, KyrolusErrorCodes.ExternalService,
                "Socket error", socketException.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        return false;
    }
}
