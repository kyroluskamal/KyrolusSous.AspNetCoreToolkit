using Microsoft.AspNetCore.Http;

namespace KyrolusSous.EndpointKit.Core.Filters;

/// <summary>
/// Minimal API endpoint filter that enforces maximum allowable request payload size early
/// via the <c>Content-Length</c> header, returning HTTP 413 Payload Too Large ProblemDetails.
/// </summary>
public sealed class KyrolusPayloadSizeEndpointFilter : IEndpointFilter
{
    private readonly long _maxSizeBytes;

    public KyrolusPayloadSizeEndpointFilter(long maxSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSizeBytes);
        _maxSizeBytes = maxSizeBytes;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var contentLength = context.HttpContext.Request.ContentLength;
        if (contentLength.HasValue && contentLength.Value > _maxSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload Too Large",
                detail: $"Request body exceeds maximum allowed size of {_maxSizeBytes} bytes.",
                type: "https://httpstatuses.com/413");
        }

        return await next(context).ConfigureAwait(false);
    }
}
