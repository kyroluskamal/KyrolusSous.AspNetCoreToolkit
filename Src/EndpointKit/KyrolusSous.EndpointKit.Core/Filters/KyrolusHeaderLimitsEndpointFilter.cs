using System.Text;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.EndpointKit.Core.Filters;

/// <summary>
/// Minimal API endpoint filter that enforces maximum allowable request header count and total size,
/// returning HTTP 431 Request Header Fields Too Large ProblemDetails upon violation.
/// </summary>
public sealed class KyrolusHeaderLimitsEndpointFilter : IEndpointFilter
{
    private readonly int _maxHeaderCount;
    private readonly int _maxTotalHeaderSizeBytes;

    public KyrolusHeaderLimitsEndpointFilter(int maxHeaderCount, int maxTotalHeaderSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeaderCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalHeaderSizeBytes);

        _maxHeaderCount = maxHeaderCount;
        _maxTotalHeaderSizeBytes = maxTotalHeaderSizeBytes;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var headers = context.HttpContext.Request.Headers;
        if (headers.Count > _maxHeaderCount)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status431RequestHeaderFieldsTooLarge,
                title: "Request Header Fields Too Large",
                detail: $"Request header count exceeded maximum allowed limit of {_maxHeaderCount}.",
                type: "https://httpstatuses.com/431");
        }

        var totalSize = 0;
        foreach (var header in headers)
        {
            totalSize += Encoding.UTF8.GetByteCount(header.Key);
            var values = header.Value;
            for (var i = 0; i < values.Count; i++)
            {
                var val = values[i];
                if (val is not null)
                {
                    totalSize += Encoding.UTF8.GetByteCount(val);
                }
            }

            if (totalSize > _maxTotalHeaderSizeBytes)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status431RequestHeaderFieldsTooLarge,
                    title: "Request Header Fields Too Large",
                    detail: $"Request total header size exceeded maximum allowed limit of {_maxTotalHeaderSizeBytes} bytes.",
                    type: "https://httpstatuses.com/431");
            }
        }

        return await next(context).ConfigureAwait(false);
    }
}
