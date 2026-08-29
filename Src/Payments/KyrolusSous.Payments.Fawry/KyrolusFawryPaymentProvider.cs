using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Fawry;

public sealed class KyrolusFawryPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusFawryOptions> options,
    ILogger<KyrolusFawryPaymentProvider>? logger = null) : IKyrolusPaymentProvider
{
    public string ProviderName => "Fawry";
    public IReadOnlyList<string> SupportedCurrencies => ["EGP", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.KioskOrRetail,
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.InstaPay
    ];

    private readonly KyrolusFawryOptions _options = options.Value;

    private string ComputeSignature(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData + _options.SecurityKey));
        return Convert.ToHexStringLower(bytes);
    }

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var customer = request.Customer;
            var formattedAmount = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            // Fawry signature: merchantCode + merchantRefNum + customerProfileId + returnUrl + itemId + quantity + price + securityKey
            var rawSig = $"{_options.MerchantCode}{request.OrderId}{customer?.CustomerId ?? "cust"}{request.SuccessUrl ?? string.Empty}ITEM-11{formattedAmount}";
            var signature = ComputeSignature(rawSig);

            var payload = new
            {
                merchantCode = _options.MerchantCode,
                merchantRefNum = request.OrderId,
                customerProfileId = customer?.CustomerId ?? "cust",
                customerName = customer?.Name ?? "Guest Customer",
                customerMobile = customer?.PhoneNumber ?? "01000000000",
                customerEmail = customer?.Email ?? "customer@example.com",
                amount = request.Amount,
                currencyCode = request.Currency.ToUpperInvariant(),
                language = "en-gb",
                chargeItems = new[]
                {
                    new
                    {
                        itemId = "ITEM-1",
                        description = request.Description ?? "Order Item",
                        price = request.Amount,
                        quantity = 1
                    }
                },
                returnUrl = request.SuccessUrl,
                signature = signature
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/ECommerceWeb/Fawry/payments/charge")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var refNumber = root.TryGetProperty("referenceNumber", out var r) ? r.GetString() : null;
                var statusCode = root.TryGetProperty("statusCode", out var sc) ? sc.GetInt32() : 200;

                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    ProviderTransactionId = refNumber,
                    Status = statusCode == 200 ? KyrolusPaymentStatus.Pending : KyrolusPaymentStatus.Failed,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ReferenceCode = refNumber,
                    RedirectUrl = $"{_options.BaseUrl}/atfawry/plugin/assets/paymentsUI.js",
                    RawDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["fawry_reference"] = refNumber ?? string.Empty
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
            logger?.LogError(ex, "Fawry CreatePayment error for Order {OrderId}", request.OrderId);
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

    public Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Succeeded });
    }

    public async Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rawSig = $"{_options.MerchantCode}{transactionId}";
            var signature = ComputeSignature(rawSig);

            var url = $"{_options.BaseUrl}/ECommerceWeb/Fawry/payments/status/v2?merchantCode={_options.MerchantCode}&merchantRefNumber={transactionId}&signature={signature}";
            var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var statusStr = doc.RootElement.TryGetProperty("paymentStatus", out var ps) ? ps.GetString() : null;
                var status = statusStr switch
                {
                    "PAID" => KyrolusPaymentStatus.Succeeded,
                    "CANCELED" or "CANCELLED" => KyrolusPaymentStatus.Cancelled,
                    "REFUNDED" => KyrolusPaymentStatus.Refunded,
                    _ => KyrolusPaymentStatus.Pending
                };

                return new KyrolusPaymentResult
                {
                    TransactionId = transactionId,
                    ProviderTransactionId = transactionId,
                    Status = status
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
            var formattedAmount = (request.Amount ?? 0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var rawSig = $"{_options.MerchantCode}{request.TransactionId}{formattedAmount}";
            var signature = ComputeSignature(rawSig);

            var payload = new
            {
                merchantCode = _options.MerchantCode,
                referenceNumber = request.TransactionId,
                refundAmount = request.Amount,
                reason = request.Reason ?? "Customer request",
                signature = signature
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/ECommerceWeb/Fawry/payments/refund")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

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
