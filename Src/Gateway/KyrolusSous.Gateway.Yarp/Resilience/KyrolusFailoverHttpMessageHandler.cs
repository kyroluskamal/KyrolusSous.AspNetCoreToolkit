namespace KyrolusSous.Gateway.Yarp.Resilience;

using System.Net;
using System.Net.Http;

/// <summary>
/// HTTP delegating handler that provides transparent retry and failover capabilities for requests handled by the gateway.
/// Defends against transient backend connectivity errors and temporary 502/503/504 server failures by retrying idempotent requests with exponential backoff.
/// </summary>
public sealed class KyrolusFailoverHttpMessageHandler : DelegatingHandler
{
    private static readonly HashSet<string> IdempotentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "PUT", "DELETE", "TRACE"
    };

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed requests. Defaults to <c>2</c>.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Gets or sets the initial backoff delay in milliseconds between retry attempts. Defaults to <c>100</c>.
    /// </summary>
    public int BackoffMs { get; set; } = 100;

    /// <summary>
    /// Gets the collection of HTTP status codes eligible for automatic retry. Defaults to 502, 503, and 504.
    /// </summary>
    public HashSet<int> RetryableStatusCodes { get; } = [
        StatusCodes.Status502BadGateway,
        StatusCodes.Status503ServiceUnavailable,
        StatusCodes.Status504GatewayTimeout
    ];

    /// <summary>
    /// Gets or sets a value indicating whether retries are strictly restricted to idempotent HTTP methods (GET, HEAD, OPTIONS, PUT, DELETE).
    /// Defaults to <c>true</c> to prevent unsafe duplicate mutations (e.g. double POST payments).
    /// </summary>
    public bool IdempotentOnly { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusFailoverHttpMessageHandler"/> class with default settings.
    /// </summary>
    public KyrolusFailoverHttpMessageHandler()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusFailoverHttpMessageHandler"/> class with an inner handler.
    /// </summary>
    /// <param name="innerHandler">The inner HTTP handler delegate.</param>
    public KyrolusFailoverHttpMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusFailoverHttpMessageHandler"/> class with custom resilience parameters.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="backoffMs">The backoff delay in milliseconds.</param>
    /// <param name="retryableStatusCodes">Optional status codes eligible for retry.</param>
    /// <param name="idempotentOnly">Whether to restrict retries to idempotent verbs.</param>
    public KyrolusFailoverHttpMessageHandler(
        int maxRetries,
        int backoffMs = 100,
        IEnumerable<int>? retryableStatusCodes = null,
        bool idempotentOnly = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        ArgumentOutOfRangeException.ThrowIfNegative(backoffMs);

        MaxRetries = maxRetries;
        BackoffMs = backoffMs;
        IdempotentOnly = idempotentOnly;

        if (retryableStatusCodes != null)
        {
            RetryableStatusCodes = [.. retryableStatusCodes];
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isIdempotent = IdempotentMethods.Contains(request.Method.Method);
        if (IdempotentOnly && !isIdempotent)
        {
            // Unsafe methods (POST, PATCH) bypass retry policy to avoid duplicate state mutations
            return await base.SendAsync(request, cancellationToken);
        }

        var attempts = 0;
        while (true)
        {
            attempts++;

            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                if (attempts <= MaxRetries && RetryableStatusCodes.Contains((int)response.StatusCode))
                {
                    response.Dispose();

                    if (BackoffMs > 0)
                    {
                        await Task.Delay(BackoffMs * attempts, cancellationToken);
                    }

                    continue;
                }

                return response;
            }
            catch (HttpRequestException) when (attempts <= MaxRetries)
            {
                if (BackoffMs > 0)
                {
                    await Task.Delay(BackoffMs * attempts, cancellationToken);
                }
            }
        }
    }
}
