using System.Text.Json;
using KyrolusSous.ExceptionHandling.Interfaces;

namespace KyrolusSous.ExceptionHandling.Writers;

public sealed class KyrolusJsonErrorResponseWriter : IKyrolusErrorResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public Task WriteAsync(HttpContext context, KyrolusExceptionMapping mapping, KyrolusErrorContext errorContext, CancellationToken cancellationToken)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)mapping.StatusCode;

        return context.Response.WriteAsync(JsonSerializer.Serialize(mapping.Error, JsonOptions), cancellationToken);
    }
}
