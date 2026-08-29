using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Tap;

public sealed class KyrolusTapPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusTapOptions> options) : IKyrolusPaymentProvider
{
    public string ProviderName => "Tap";
    public IReadOnlyList<string> SupportedCurrencies => ["SAR", "KWD", "AED", "BHD", "QAR", "OMR", "EGP", "USD", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.BuyNowPayLater
    ];

    private readonly KyrolusTapOptions _options = options.Value;

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var customer = request.Customer;
            var payload = new
            {
                amount = request.Amount,
                currency = request.Currency.ToUpperInvariant(),
                threeDSecure = true,
                save_card = false,
                description = request.Description ?? $"Order {request.OrderId}",
                reference = new
                {
                    transaction = request.OrderId,
                    order = request.OrderId
                },
                customer = new
                {
                    first_name = customer?.Name?.Split(' ').FirstOrDefault() ?? "Customer",
                    email = customer?.Email ?? "customer@example.com",
                    phone = new
                    {
                        country_code = "966",
                        number = customer?.PhoneNumber ?? "500000000"
                    }
                },
                source = new { id = "src_all" },
                redirect = new { url = request.SuccessUrl ?? "https://example.com/redirect" }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/charges")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var chargeId = root.GetProperty("id").GetString()!;
                var statusStr = root.GetProperty("status").GetString()!;

                string? redirectUrl = null;
                if (root.TryGetProperty("transaction", out var tx) && tx.TryGetProperty("url", out var u))
                {
                    redirectUrl = u.GetString();
                }

                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    ProviderTransactionId = chargeId,
                    Status = statusStr == "CAPTURED" ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.RequiresAction,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    RedirectUrl = redirectUrl
                };
            }

            return new KyrolusPaymentResult { TransactionId = request.OrderId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = content };
        }
        catch (Exception ex)
        {
            return new KyrolusPaymentResult { TransactionId = request.OrderId, Status = KyrolusPaymentStatus.Failed, ErrorMessage = ex.Message };
        }
    }

    public Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Succeeded });
    }

    public async Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/charges/{transactionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var statusStr = doc.RootElement.GetProperty("status").GetString();
                var status = statusStr switch
                {
                    "CAPTURED" => KyrolusPaymentStatus.Succeeded,
                    "DECLINED" or "FAILED" => KyrolusPaymentStatus.Failed,
                    "CANCELLED" => KyrolusPaymentStatus.Cancelled,
                    _ => KyrolusPaymentStatus.Pending
                };

                return new KyrolusPaymentResult { TransactionId = transactionId, ProviderTransactionId = transactionId, Status = status };
            }

            return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed };
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
                charge_id = request.TransactionId,
                amount = request.Amount,
                currency = (request.Currency ?? "SAR").ToUpperInvariant(),
                reason = request.Reason ?? "Customer requested refund"
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var id = doc.RootElement.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                return new KyrolusRefundResult { RefundId = id, TransactionId = request.TransactionId, Succeeded = true, RefundedAmount = request.Amount };
            }

            return new KyrolusRefundResult { RefundId = string.Empty, TransactionId = request.TransactionId, Succeeded = false, ErrorMessage = content };
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
