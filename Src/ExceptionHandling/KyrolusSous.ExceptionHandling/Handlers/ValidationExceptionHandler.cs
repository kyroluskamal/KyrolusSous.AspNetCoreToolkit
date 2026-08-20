using System.Net;
using FluentValidation;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.ExceptionHandling.Handlers;

public class ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException valEx)
        {
            var errors = valEx.Errors.Select(e => new KyrolusErrorItem(e.PropertyName, e.ErrorCode, e.ErrorMessage)).ToArray();
            await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
                logger, httpContext, HttpStatusCode.BadRequest, KyrolusErrorCodes.Validation,
                "Validation failed", valEx.Message, errors, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        return false;
    }
}
