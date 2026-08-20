using System.Net;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.ExceptionHandling.Handlers;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
    public UnauthorizedException(string entityName, string key) : base($"{entityName} with key {key} not found") { }
}

public class UnauthorizedExceptionHandler(ILogger<UnauthorizedExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is UnauthorizedException unauthorizedException)
        {
            await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
                logger, httpContext, HttpStatusCode.Unauthorized, KyrolusErrorCodes.Unauthorized,
                "Unauthorized", unauthorizedException.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        return false;
    }
}
