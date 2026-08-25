using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.EndpointKit.Core.Streaming;

/// <summary>
/// Server-Sent Events (SSE) IResult for real-time live streaming from IAsyncEnumerable sources.
/// </summary>
public sealed class KyrolusSseResult<T>(
    IAsyncEnumerable<T> stream,
    string? eventType = null,
    JsonSerializerOptions? jsonOptions = null) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var cancellationToken = httpContext.RequestAborted;
        var options = jsonOptions ?? JsonSerializerOptions.Default;

        try
        {
            await using var writer = new StreamWriter(httpContext.Response.Body, leaveOpen: true);

            await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (!string.IsNullOrWhiteSpace(eventType))
                {
                    await writer.WriteAsync($"event: {eventType}\n");
                }

                var json = JsonSerializer.Serialize(item, options);
                await writer.WriteAsync($"data: {json}\n\n");
                await writer.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client gracefully disconnected
        }
    }
}
