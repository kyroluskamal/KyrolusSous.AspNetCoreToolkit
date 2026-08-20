using System.Net;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace KyrolusSous.ExceptionHandling.Handlers;

public class NpgsqlExceptionHandler(ILogger<NpgsqlExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is NpgsqlException npgsqlException)
        {
            await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
                logger, httpContext, HttpStatusCode.InternalServerError, KyrolusErrorCodes.InternalError,
                "Database error", npgsqlException.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        return false;
    }
}
