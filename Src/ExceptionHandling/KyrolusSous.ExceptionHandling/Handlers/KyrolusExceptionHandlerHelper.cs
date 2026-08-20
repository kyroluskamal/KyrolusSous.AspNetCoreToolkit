using System.Net;
using System.Text.Json;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.ExceptionHandling.Handlers;

internal static class KyrolusExceptionHandlerHelper
{
    public static async ValueTask WriteEnvelopeAsync(
        ILogger logger,
        HttpContext httpContext,
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogError(
            "Exception handled: {Code} ({StatusCode}). Path={Path}, Message={Message}",
            code, (int)statusCode, httpContext.Request.Path, detail);

        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = (int)statusCode;

        var envelope = new KyrolusErrorEnvelope(
            Code: code,
            Title: title,
            Detail: detail,
            TraceId: httpContext.TraceIdentifier,
            Errors: errors);

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            envelope,
            KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope,
            cancellationToken).ConfigureAwait(false);
    }
}
