using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusPayPalPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusPayPalOptions> options,
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

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
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
                            value = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v2/checkout/orders/{transactionId}/capture")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
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
                ? new { amount = new { value = request.Amount.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), currency_code = (request.Currency ?? "USD").ToUpperInvariant() } }
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
