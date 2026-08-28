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

public sealed class KyrolusHmacSigner : IKyrolusHmacSigner
{
    public string ComputeSignature(string secretKey, string timestamp, string httpMethod, string pathAndQuery, byte[]? body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        var bodyHash = body is { Length: > 0 } ? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(body)) : string.Empty;
        var payload = $"{timestamp}:{httpMethod.ToUpperInvariant()}:{pathAndQuery}:{bodyHash}";

        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secretKey));
        var signatureBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(signatureBytes);
    }

    public bool VerifySignature(string secretKey, string signature, string timestamp, string httpMethod, string pathAndQuery, byte[]? body)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            return false;
        }

        var expectedSignature = ComputeSignature(secretKey, timestamp, httpMethod, pathAndQuery, body);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(signature),
            System.Text.Encoding.UTF8.GetBytes(expectedSignature));
    }
}

public sealed class KyrolusHmacDelegatingHandler(IKyrolusHmacSigner signer, KyrolusHmacOptions options) : DelegatingHandler
{
    private readonly IKyrolusHmacSigner _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    private readonly KyrolusHmacOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var method = request.Method.Method;
        var pathAndQuery = request.RequestUri?.PathAndQuery ?? "/";

        byte[]? bodyBytes = null;
        if (request.Content is not null)
        {
            bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        var signature = _signer.ComputeSignature(_options.SecretKey, timestamp, method, pathAndQuery, bodyBytes);

        request.Headers.Remove(_options.HeaderName);
        request.Headers.Remove(_options.TimestampHeaderName);
        request.Headers.Add(_options.HeaderName, signature);
        request.Headers.Add(_options.TimestampHeaderName, timestamp);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
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

        services.AddSingleton<IKyrolusHmacSigner, KyrolusHmacSigner>();
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
