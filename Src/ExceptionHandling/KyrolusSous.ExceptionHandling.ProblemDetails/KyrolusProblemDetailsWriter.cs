global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using KyrolusSous.ExceptionHandling.Runtime.Interfaces;
global using Microsoft.AspNetCore.Http;
global using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
global using System.Text.Json;
global using System.Text.Json.Serialization;

namespace KyrolusSous.ExceptionHandling.ProblemDetails;

public sealed class KyrolusProblemDetailsWriter : IKyrolusErrorResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public Task WriteAsync(HttpContext context, KyrolusExceptionMapping mapping, KyrolusErrorContext errorContext, CancellationToken cancellationToken)
    {
        var problemType = BuildProblemType(mapping.Error.Code);
        var details = new MvcProblemDetails
        {
            Status = (int)mapping.StatusCode,
            Title = mapping.Error.Title,
            Detail = mapping.Error.Detail,
            Type = problemType,
            Instance = context.Request.Path
        };

        details.Extensions["code"] = mapping.Error.Code;
        details.Extensions["traceId"] = mapping.Error.TraceId;
        details.Extensions["errors"] = mapping.Error.Errors;

        if (mapping.Error.Metadata is not null)
        {
            foreach (var (key, value) in mapping.Error.Metadata)
            {
                details.Extensions[key] = value;
            }
        }

        context.Response.StatusCode = details.Status ?? (int)mapping.StatusCode;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsync(JsonSerializer.Serialize(details, JsonOptions), cancellationToken);
    }

    private static string BuildProblemType(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "about:blank";
        }

        if (Uri.TryCreate(code, UriKind.Absolute, out _))
        {
            return code;
        }

        return $"urn:kyrolus:error:{code}";
    }
}
