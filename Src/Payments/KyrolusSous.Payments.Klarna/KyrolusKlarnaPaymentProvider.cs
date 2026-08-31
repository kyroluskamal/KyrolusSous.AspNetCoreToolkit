using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Klarna;

public sealed class KyrolusKlarnaPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusKlarnaOptions> options) : IKyrolusPaymentProvider
{
    public string ProviderName => "Klarna";
    public IReadOnlyList<string> SupportedCurrencies => ["EUR", "USD", "GBP", "SEK", "NOK", "DKK", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.BuyNowPayLater,
        KyrolusPaymentMethodType.DirectDebit,
        KyrolusPaymentMethodType.CreditCard
    ];

    private readonly KyrolusKlarnaOptions _options = options.Value;

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var minorUnits = (long)Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero);
            var payload = new
            {
                purchase_country = request.Customer?.CountryCode ?? "SE",
                purchase_currency = request.Currency.ToUpperInvariant(),
                order_amount = minorUnits,
                order_lines = new[]
                {
                    new
                    {
                        name = request.Description ?? $"Order {request.OrderId}",
                        quantity = 1,
                        unit_price = minorUnits,
                        total_amount = minorUnits
                    }
                }
            };

            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiUsername}:{_options.ApiPassword}"));
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/payments/v1/sessions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var sessionId = doc.RootElement.GetProperty("session_id").GetString()!;
                var clientToken = doc.RootElement.GetProperty("client_token").GetString()!;

                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    ProviderTransactionId = sessionId,
                    Status = KyrolusPaymentStatus.RequiresAction,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    RawDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["client_token"] = clientToken,
                        ["session_id"] = sessionId
                    }
                };
            }

            return new KyrolusPaymentResult { TransactionId = request.OrderId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = content };
        }
        catch (Exception ex)
        {
            return new KyrolusPaymentResult { TransactionId = request.OrderId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = ex.Message };
        }
    }

    private AuthenticationHeaderValue BasicAuthHeader() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiUsername}:{_options.ApiPassword}")));

    // Uses transactionId as the Klarna Order Management order id, consistent with how the rest of
    // this provider treats it. Callers still need to "place" the order via Klarna's checkout flow
    // before order-management operations below are valid for that id.
    public async Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = amount.HasValue
                ? new { captured_amount = (long)Math.Round(amount.Value * 100, MidpointRounding.AwayFromZero) }
                : (object)new { };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/ordermanagement/v1/orders/{transactionId}/captures")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = BasicAuthHeader();

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Succeeded };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = content };
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/ordermanagement/v1/orders/{transactionId}");
            httpRequest.Headers.Authorization = BasicAuthHeader();

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var capturedAmount = root.TryGetProperty("captured_amount", out var ca) ? ca.GetInt64() : 0;
            var refundedAmount = root.TryGetProperty("refunded_amount", out var ra) ? ra.GetInt64() : 0;

            var status = refundedAmount > 0
                ? KyrolusPaymentStatus.Refunded
                : capturedAmount > 0
                    ? KyrolusPaymentStatus.Succeeded
                    : KyrolusPaymentStatus.Pending;

            return new KyrolusPaymentResult { TransactionId = transactionId, ProviderTransactionId = transactionId, Status = status };
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
            var payload = new
            {
                refunded_amount = (long)Math.Round((request.Amount ?? 0) * 100, MidpointRounding.AwayFromZero),
                description = request.Reason ?? "Refund request"
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/ordermanagement/v1/orders/{request.TransactionId}/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = BasicAuthHeader();

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new KyrolusRefundResult
                {
                    RefundId = Guid.NewGuid().ToString("N"),
                    TransactionId = request.TransactionId,
                    Succeeded = true,
                    RefundedAmount = request.Amount
                };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new KyrolusRefundResult { RefundId = string.Empty, TransactionId = request.TransactionId, Succeeded = false, ErrorMessage = content };
        }
        catch (Exception ex)
        {
            return new KyrolusRefundResult { RefundId = string.Empty, TransactionId = request.TransactionId, Succeeded = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<bool> CancelPaymentAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/ordermanagement/v1/orders/{transactionId}/cancel");
            httpRequest.Headers.Authorization = BasicAuthHeader();
            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
