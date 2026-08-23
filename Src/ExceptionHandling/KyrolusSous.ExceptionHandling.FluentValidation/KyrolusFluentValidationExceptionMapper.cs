global using System.Net;
global using FluentValidation;
global using KyrolusSous.ExceptionHandling.Abstractions.Helpers;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;

namespace KyrolusSous.ExceptionHandling.FluentValidation;

public sealed class KyrolusFluentValidationExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -50;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is not ValidationException validationException)
        {
            mapping = null!;
            return false;
        }

        var errors = validationException.Errors
            .Select(error => new KyrolusErrorItem(error.PropertyName, error.ErrorCode ?? "validation_error", error.ErrorMessage))
            .ToArray();

        mapping = KyrolusExceptionMapping.Create(
            code: KyrolusErrorCodes.Validation,
            title: "Validation failed",
            statusCode: HttpStatusCode.BadRequest,
            detail: "One or more validation errors occurred.",
            traceId: context.TraceId,
            errors: [.. errors],
            metadata: KyrolusMetadataExtractor.Extract(validationException))
            .WithoutLogging();

        return true;
    }
}
