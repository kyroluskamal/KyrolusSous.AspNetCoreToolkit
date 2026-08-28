using System.Net.Http.Headers;
using KyrolusSous.Http.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Http.Core;

public sealed class KyrolusAuthDelegatingHandler(IKyrolusTokenPropagator? tokenPropagator = null) : DelegatingHandler
{
    private readonly IKyrolusTokenPropagator? _tokenPropagator = tokenPropagator;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_tokenPropagator is not null && request.Headers.Authorization is null)
        {
            var token = await _tokenPropagator.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class KyrolusCorrelationDelegatingHandler(IKyrolusCorrelationPropagator? correlationPropagator = null) : DelegatingHandler
{
    private const string CorrelationHeader = "X-Correlation-ID";
    private readonly IKyrolusCorrelationPropagator? _correlationPropagator = correlationPropagator;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _correlationPropagator?.GetCorrelationId() ?? Guid.NewGuid().ToString("N");
        if (!request.Headers.Contains(CorrelationHeader))
        {
            request.Headers.Add(CorrelationHeader, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class KyrolusLoggingDelegatingHandler(ILogger<KyrolusLoggingDelegatingHandler>? logger = null) : DelegatingHandler
{
    private readonly ILogger<KyrolusLoggingDelegatingHandler>? _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method;
        var uri = request.RequestUri;

        _logger?.LogDebug("Sending HTTP request: {Method} {Uri}", method, uri);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            _logger?.LogInformation("Received HTTP response: {StatusCode} from {Method} {Uri} in {ElapsedMs}ms",
                (int)response.StatusCode, method, uri, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "HTTP request failed: {Method} {Uri} after {ElapsedMs}ms", method, uri, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddKyrolusHttpClient<TClient, TImplementation>(this IServiceCollection services, Action<KyrolusHttpClientOptions>? configure = null)
        where TClient : class
        where TImplementation : class, TClient
    {
        var options = new KyrolusHttpClientOptions();
        configure?.Invoke(options);

        services.AddTransient<KyrolusAuthDelegatingHandler>();
        services.AddTransient<KyrolusCorrelationDelegatingHandler>();
        services.AddTransient<KyrolusLoggingDelegatingHandler>();

        var builder = services.AddHttpClient<TClient, TImplementation>((sp, client) =>
        {
            if (!string.IsNullOrEmpty(options.BaseAddress))
            {
                client.BaseAddress = new Uri(options.BaseAddress);
            }
            client.Timeout = options.Timeout;
        });

        if (options.PropagateCorrelationId)
        {
            builder.AddHttpMessageHandler<KyrolusCorrelationDelegatingHandler>();
        }

        if (options.PropagateAuthToken)
        {
            builder.AddHttpMessageHandler<KyrolusAuthDelegatingHandler>();
        }

        builder.AddHttpMessageHandler<KyrolusLoggingDelegatingHandler>();
        return builder;
    }
}
