using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Checkout;

public sealed class KyrolusCheckoutPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusCheckoutOptions> options) : IKyrolusPaymentProvider
{
    public string ProviderName => "Checkout";
    public IReadOnlyList<string> SupportedCurrencies => ["GBP", "USD", "EUR", "AED", "SAR", "EGP", "CAD", "AUD", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.BuyNowPayLater
    ];

    private readonly KyrolusCheckoutOptions _options = options.Value;

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var minorUnits = (long)Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero);
            var payload = new
            {
                source = new { type = "hosted" },
                amount = minorUnits,
                currency = request.Currency.ToUpperInvariant(),
                reference = request.OrderId,
                description = request.Description ?? $"Order {request.OrderId}",
                success_url = request.SuccessUrl ?? "https://example.com/success",
                cancel_url = request.CancelUrl ?? "https://example.com/cancel",
                customer = new
                {
                    email = request.Customer?.Email ?? "customer@example.com",
                    name = request.Customer?.Name ?? "Customer"
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/hosted-payments")
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
                var id = root.GetProperty("id").GetString()!;
                var statusStr = root.TryGetProperty("status", out var s) ? s.GetString() : "Payment Received";

                string? redirectUrl = null;
                if (root.TryGetProperty("_links", out var links) && links.TryGetProperty("redirect", out var red))
                {
                    redirectUrl = red.GetProperty("href").GetString();
                }

                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    ProviderTransactionId = id,
                    Status = KyrolusPaymentStatus.RequiresAction,
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

    public async Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = amount.HasValue ? (object)new { amount = (long)(amount.Value * 100) } : new { };
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/payments/{transactionId}/captures")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return new KyrolusPaymentResult
            {
                TransactionId = transactionId,
                Status = response.IsSuccessStatusCode ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Failed
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/payments/{transactionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var statusStr = doc.RootElement.GetProperty("status").GetString();
                var status = statusStr switch
                {
                    "Captured" or "Authorized" => KyrolusPaymentStatus.Succeeded,
                    "Declined" => KyrolusPaymentStatus.Failed,
                    "Canceled" => KyrolusPaymentStatus.Cancelled,
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
            var payload = request.Amount.HasValue
                ? (object)new { amount = (long)(request.Amount.Value * 100), reference = request.Reason ?? "Refund" }
                : new { reference = request.Reason ?? "Refund" };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/payments/{request.TransactionId}/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var id = doc.RootElement.TryGetProperty("action_id", out var a) ? a.GetString() : Guid.NewGuid().ToString("N");
                return new KyrolusRefundResult { RefundId = id ?? Guid.NewGuid().ToString("N"), TransactionId = request.TransactionId, Succeeded = true, RefundedAmount = request.Amount };
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
