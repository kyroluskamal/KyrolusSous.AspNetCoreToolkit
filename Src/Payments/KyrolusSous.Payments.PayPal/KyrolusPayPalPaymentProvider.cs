using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusPayPalPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusPayPalOptions> options,
    IKyrolusCacheProvider? cacheProvider = null,
    ILogger<KyrolusPayPalPaymentProvider>? logger = null) : IKyrolusPaymentProvider
{
    public string ProviderName => "PayPal";
    public IReadOnlyList<string> SupportedCurrencies => ["USD", "EUR", "GBP", "CAD", "AUD", "JPY", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.BuyNowPayLater
    ];

    private readonly KyrolusPayPalOptions _options = options.Value;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    // PayPal rejects amounts with more decimal places than the currency supports (e.g. JPY must be
    // sent as "1000", not "1000.00").
    private static string FormatAmount(decimal amount, string currency) =>
        amount.ToString("F" + KyrolusCurrencyHelper.GetDecimalPlaces(currency), System.Globalization.CultureInfo.InvariantCulture);

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var cacheKey = $"kyrolus:paypal:token:{_options.ClientId}";
        if (cacheProvider is not null)
        {
            var cached = await cacheProvider.GetAsync<string>(cacheKey, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(cached)) return cached;
        }

        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        var authBytes = Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}");
        var authHeader = Convert.ToBase64String(authBytes);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")])
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(content);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

        if (cacheProvider is not null)
        {
            await cacheProvider.SetAsync(cacheKey, _accessToken, TimeSpan.FromSeconds(Math.Max(10, expiresIn - 60)), cancellationToken).ConfigureAwait(false);
        }

        return _accessToken;
    }

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            var payload = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = request.OrderId,
                        description = request.Description ?? $"Order {request.OrderId}",
                        amount = new
                        {
                            currency_code = request.Currency.ToUpperInvariant(),
                            value = FormatAmount(request.Amount, request.Currency)
                        }
                    }
                },
                application_context = new
                {
                    return_url = request.SuccessUrl,
                    cancel_url = request.CancelUrl
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v2/checkout/orders")
            {
                Content = jsonContent
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger?.LogError("PayPal CreatePayment failed: {Response}", responseJson);
                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    Status = KyrolusPaymentStatus.Failed,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ErrorMessage = responseJson
                };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var orderId = root.GetProperty("id").GetString()!;
            var statusStr = root.GetProperty("status").GetString()!;

            string? approveLink = null;
            if (root.TryGetProperty("links", out var links))
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (link.GetProperty("rel").GetString() == "approve")
                    {
                        approveLink = link.GetProperty("href").GetString();
                        break;
                    }
                }
            }

            return new KyrolusPaymentResult
            {
                TransactionId = request.OrderId,
                ProviderTransactionId = orderId,
                Status = statusStr == "COMPLETED" ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Pending,
                Amount = request.Amount,
                Currency = request.Currency,
                RedirectUrl = approveLink
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Exception in PayPal CreatePayment for Order {OrderId}", request.OrderId);
            return new KyrolusPaymentResult
            {
                TransactionId = request.OrderId,
                Status = KyrolusPaymentStatus.Failed,
                Amount = request.Amount,
                Currency = request.Currency,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            // A partial capture requires the order's currency, which this method isn't given -
            // look it up so the requested amount isn't silently dropped in favor of a full capture.
            object body = new { };
            if (amount.HasValue)
            {
                var orderRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/v2/checkout/orders/{transactionId}");
                orderRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var orderResponse = await httpClient.SendAsync(orderRequest, cancellationToken).ConfigureAwait(false);
                var orderContent = await orderResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!orderResponse.IsSuccessStatusCode)
                {
                    return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = orderContent };
                }

                using var orderDoc = JsonDocument.Parse(orderContent);
                var currencyCode = orderDoc.RootElement.GetProperty("purchase_units")[0].GetProperty("amount").GetProperty("currency_code").GetString()!;
                body = new { amount = new { value = FormatAmount(amount.Value, currencyCode), currency_code = currencyCode } };
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v2/checkout/orders/{transactionId}/capture")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new KyrolusPaymentResult
                {
                    TransactionId = transactionId,
                    ProviderTransactionId = transactionId,
                    Status = KyrolusPaymentStatus.Succeeded
                };
            }

            return new KyrolusPaymentResult
            {
                TransactionId = transactionId,
                Status = KyrolusPaymentStatus.Failed,
                ErrorMessage = content
            };
        }
        catch (Exception ex)
        {
            return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = ex.Message };
        }
    }

    public async Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/v2/checkout/orders/{transactionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed };
            }

            using var doc = JsonDocument.Parse(content);
            var statusStr = doc.RootElement.GetProperty("status").GetString();

            var status = statusStr switch
            {
                "COMPLETED" or "APPROVED" => KyrolusPaymentStatus.Succeeded,
                "VOIDED" => KyrolusPaymentStatus.Cancelled,
                _ => KyrolusPaymentStatus.Pending
            };

            return new KyrolusPaymentResult
            {
                TransactionId = transactionId,
                ProviderTransactionId = transactionId,
                Status = status
            };
        }
        catch (Exception ex)
        {
            return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = ex.Message };
        }
    }

    public async Task<KyrolusRefundResult> RefundPaymentAsync(KyrolusRefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var payload = request.Amount.HasValue
                ? new { amount = new { value = FormatAmount(request.Amount.Value, request.Currency ?? "USD"), currency_code = (request.Currency ?? "USD").ToUpperInvariant() } }
                : (object)new { };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v2/payments/captures/{request.TransactionId}/refund")
            {
                Content = jsonContent
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var refundId = doc.RootElement.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                return new KyrolusRefundResult
                {
                    RefundId = refundId,
                    TransactionId = request.TransactionId,
                    Succeeded = true,
                    RefundedAmount = request.Amount
                };
            }

            return new KyrolusRefundResult
            {
                RefundId = string.Empty,
                TransactionId = request.TransactionId,
                Succeeded = false,
                ErrorMessage = content
            };
        }
        catch (Exception ex)
        {
            return new KyrolusRefundResult { RefundId = string.Empty, TransactionId = request.TransactionId, Succeeded = false, ErrorMessage = ex.Message };
        }
    }

    public Task<bool> CancelPaymentAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
