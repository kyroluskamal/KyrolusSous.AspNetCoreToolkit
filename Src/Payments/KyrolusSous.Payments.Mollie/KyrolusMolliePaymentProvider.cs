using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Mollie;

public sealed class KyrolusMolliePaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusMollieOptions> options) : IKyrolusPaymentProvider
{
    public string ProviderName => "Mollie";
    public IReadOnlyList<string> SupportedCurrencies => ["EUR", "USD", "GBP", "CHF", "SEK", "NOK", "DKK", "PLN", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.BankTransfer,
        KyrolusPaymentMethodType.DirectDebit,
        KyrolusPaymentMethodType.DigitalWallet
    ];

    private readonly KyrolusMollieOptions _options = options.Value;

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                amount = new
                {
                    currency = request.Currency.ToUpperInvariant(),
                    value = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                },
                description = request.Description ?? $"Order {request.OrderId}",
                redirectUrl = request.SuccessUrl ?? "https://example.com/redirect",
                webhookUrl = request.WebhookUrl,
                metadata = new { order_id = request.OrderId }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/payments")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var paymentId = root.GetProperty("id").GetString()!;
                var statusStr = root.GetProperty("status").GetString()!;

                string? checkoutUrl = null;
                if (root.TryGetProperty("_links", out var links) && links.TryGetProperty("checkout", out var chk))
                {
                    checkoutUrl = chk.GetProperty("href").GetString();
                }

                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    ProviderTransactionId = paymentId,
                    Status = statusStr == "paid" ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.RequiresAction,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    RedirectUrl = checkoutUrl
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/payments/{transactionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var statusStr = doc.RootElement.GetProperty("status").GetString();
                var status = statusStr switch
                {
                    "paid" => KyrolusPaymentStatus.Succeeded,
                    "canceled" => KyrolusPaymentStatus.Cancelled,
                    "expired" or "failed" => KyrolusPaymentStatus.Failed,
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
            // Mollie treats an omitted `amount` as "refund the full remaining balance" - sending an
            // explicit 0.00 instead would ask Mollie to refund nothing.
            object payload;
            if (request.Amount.HasValue)
            {
                payload = new
                {
                    amount = new
                    {
                        currency = (request.Currency ?? "EUR").ToUpperInvariant(),
                        value = request.Amount.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    },
                    description = request.Reason ?? "Refund request"
                };
            }
            else
            {
                payload = new { description = request.Reason ?? "Refund request" };
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/payments/{request.TransactionId}/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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
