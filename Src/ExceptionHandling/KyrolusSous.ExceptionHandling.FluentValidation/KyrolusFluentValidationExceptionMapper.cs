global using System.Net;
global using FluentValidation;
global using KyrolusSous.ExceptionHandling.Abstractions.Helpers;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using Microsoft.Extensions.Options;

namespace KyrolusSous.ExceptionHandling.FluentValidation;

/// <summary>
/// Translates FluentValidation <see cref="ValidationException"/> instances into structured RFC 7807 validation problem mappings
/// with smart dynamic details and configurable options.
/// </summary>
/// <remarks>
/// Automatically extracts all <see cref="ValidationException.Errors"/>, maps property names, error codes, and messages into <see cref="KyrolusErrorItem"/>,
/// sets HTTP 400 Bad Request, and suppresses server logging to avoid polluting logs with routine user validation failures.
/// </remarks>
/// <example>
/// <code>
/// // Registration in Program.cs:
/// builder.Services.AddKyrolusFluentValidationExceptionHandling();
/// </code>
/// </example>
public sealed class KyrolusFluentValidationExceptionMapper(IOptions<KyrolusFluentValidationOptions>? options = null) : IKyrolusExceptionMapper
{
    private readonly KyrolusFluentValidationOptions _options = options?.Value ?? new KyrolusFluentValidationOptions();

    /// <summary>
    /// Gets the mapper order (-50 to execute ahead of general fallback mappers).
    /// </summary>
    public int Order => -50;

    /// <summary>
    /// Attempts to map FluentValidation <see cref="ValidationException"/> into <see cref="KyrolusExceptionMapping"/>.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <param name="context">Ambient request context.</param>
    /// <param name="mapping">The mapped error result.</param>
    /// <returns><c>true</c> if mapped; otherwise, <c>false</c>.</returns>
    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is not ValidationException validationException)
        {
            mapping = null!;
            return false;
        }

        var errors = validationException.Errors?
            .Where(e => e is not null)
            .Select(error => new KyrolusErrorItem(error.PropertyName, error.ErrorCode ?? "validation_error", error.ErrorMessage))
            .ToArray() ?? [];

        var detail = ResolveDetail(validationException, errors);

        mapping = KyrolusExceptionMapping.Create(
            code: KyrolusErrorCodes.Validation,
            title: _options.DefaultTitle,
            statusCode: HttpStatusCode.BadRequest,
            detail: detail,
            traceId: context?.TraceId,
            errors: [.. errors],
            metadata: KyrolusMetadataExtractor.Extract(validationException))
            .WithoutLogging();

        return true;
    }

    private string? ResolveDetail(ValidationException validationException, KyrolusErrorItem[] errors)
    {
        if (_options.DetailFormatter is not null)
        {
            return _options.DetailFormatter(validationException, errors);
        }

        if (!_options.EnableDynamicDetail)
        {
            return "One or more validation errors occurred.";
        }

        return errors.Length switch
        {
            0 => "Validation failed.",
            1 => !string.IsNullOrWhiteSpace(errors[0].Field)
                ? $"Validation failed on '{errors[0].Field}': {errors[0].Message ?? "Invalid value."}"
                : errors[0].Message ?? "Validation failed.",
            _ => $"{errors.Length} validation errors occurred in fields: {string.Join(", ", errors.Select(e => e.Field).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct())}."
        };
    }
}
