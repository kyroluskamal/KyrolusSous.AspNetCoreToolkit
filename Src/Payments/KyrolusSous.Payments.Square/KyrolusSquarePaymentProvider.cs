using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Square;

public sealed class KyrolusSquarePaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusSquareOptions> options) : IKyrolusPaymentProvider
{
    public string ProviderName => "Square";
    public IReadOnlyList<string> SupportedCurrencies => ["USD", "CAD", "GBP", "AUD", "JPY", "EUR", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.BuyNowPayLater
    ];

    private readonly KyrolusSquareOptions _options = options.Value;

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var amountCents = (long)Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero);
            var payload = new
            {
                idempotency_key = Guid.NewGuid().ToString("N"),
                location_id = _options.LocationId,
                order = new
                {
                    reference_id = request.OrderId,
                    location_id = _options.LocationId,
                    line_items = new[]
                    {
                        new
                        {
                            name = request.Description ?? $"Order {request.OrderId}",
                            quantity = "1",
                            base_price_money = new
                            {
                                amount = amountCents,
                                currency = request.Currency.ToUpperInvariant()
                            }
                        }
                    }
                },
                redirect_url = request.SuccessUrl
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/online-checkout/payment-links")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var paymentLink = doc.RootElement.GetProperty("payment_link");
                var id = paymentLink.GetProperty("id").GetString()!;
                var url = paymentLink.GetProperty("url").GetString()!;

                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    ProviderTransactionId = id,
                    Status = KyrolusPaymentStatus.RequiresAction,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    RedirectUrl = url
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
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var statusStr = doc.RootElement.GetProperty("payment").GetProperty("status").GetString();
                return new KyrolusPaymentResult
                {
                    TransactionId = transactionId,
                    Status = statusStr == "COMPLETED" ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Pending
                };
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
            var amountCents = (long)Math.Round((request.Amount ?? 0) * 100);
            var payload = new
            {
                idempotency_key = Guid.NewGuid().ToString("N"),
                payment_id = request.TransactionId,
                amount_money = new
                {
                    amount = amountCents,
                    currency = (request.Currency ?? "USD").ToUpperInvariant()
                },
                reason = request.Reason ?? "Customer refund"
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var id = doc.RootElement.GetProperty("refund").GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
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
