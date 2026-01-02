global using FluentValidation;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using System.Net;

namespace KyrolusSous.ExceptionHandling.FluentValidation;

public sealed class KyrolusFluentValidationExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -50;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .Select(error => new KyrolusErrorItem(error.PropertyName, error.ErrorCode, error.ErrorMessage))
                .ToArray();

            mapping = new KyrolusExceptionMapping(
                new KyrolusErrorEnvelope(
                    KyrolusErrorCodes.Validation,
                    "Validation failed",
                    validationException.Message,
                    context.TraceId,
                    errors),
                HttpStatusCode.BadRequest);
            return true;
        }

        mapping = default!;
        return false;
    }
}
