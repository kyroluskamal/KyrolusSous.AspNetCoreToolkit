using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Adyen;

public sealed class KyrolusAdyenPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusAdyenOptions> options,
    ILogger<KyrolusAdyenPaymentProvider>? logger = null) : IKyrolusPaymentProvider
{
    public string ProviderName => "Adyen";
    public IReadOnlyList<string> SupportedCurrencies => ["EUR", "USD", "GBP", "CHF", "SEK", "NOK", "DKK", "PLN", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.DirectDebit,
        KyrolusPaymentMethodType.BuyNowPayLater,
        KyrolusPaymentMethodType.BankTransfer
    ];

    private readonly KyrolusAdyenOptions _options = options.Value;

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var minorUnits = (long)Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero);
            var payload = new
            {
                amount = new
                {
                    currency = request.Currency.ToUpperInvariant(),
                    value = minorUnits
                },
                reference = request.OrderId,
                merchantAccount = _options.MerchantAccount,
                returnUrl = request.SuccessUrl ?? "https://example.com/return",
                countryCode = request.Customer?.CountryCode ?? "NL",
                shopperEmail = request.Customer?.Email,
                shopperReference = request.Customer?.CustomerId ?? request.OrderId
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/sessions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("X-API-Key", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var sessionId = root.GetProperty("id").GetString()!;
                var sessionData = root.GetProperty("sessionData").GetString()!;
                var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;

                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    ProviderTransactionId = sessionId,
                    Status = KyrolusPaymentStatus.RequiresAction,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    RedirectUrl = url,
                    RawDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sessionData"] = sessionData,
                        ["sessionId"] = sessionId
                    }
                };
            }

            return new KyrolusPaymentResult
            {
                TransactionId = request.OrderId,
                Status = KyrolusPaymentStatus.Failed,
                Amount = request.Amount,
                Currency = request.Currency,
                ErrorMessage = content
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Adyen CreatePayment error for Order {OrderId}", request.OrderId);
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
            var payload = new
            {
                merchantAccount = _options.MerchantAccount,
                amount = amount.HasValue ? (object)new { currency = "EUR", value = (long)(amount.Value * 100) } : null,
                reference = $"capture_{transactionId}"
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/payments/{transactionId}/captures")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("X-API-Key", _options.ApiKey);

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

    public Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Succeeded });
    }

    public async Task<KyrolusRefundResult> RefundPaymentAsync(KyrolusRefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                merchantAccount = _options.MerchantAccount,
                amount = new
                {
                    currency = (request.Currency ?? "EUR").ToUpperInvariant(),
                    value = (long)((request.Amount ?? 0) * 100)
                },
                reference = $"refund_{request.TransactionId}"
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/payments/{request.TransactionId}/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("X-API-Key", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var pspReference = doc.RootElement.GetProperty("pspReference").GetString() ?? Guid.NewGuid().ToString("N");
                return new KyrolusRefundResult
                {
                    RefundId = pspReference,
                    TransactionId = request.TransactionId,
                    Succeeded = true,
                    RefundedAmount = request.Amount
                };
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
