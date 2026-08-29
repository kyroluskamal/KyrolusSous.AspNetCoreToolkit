using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Paymob;

public sealed class KyrolusPaymobPaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusPaymobOptions> options,
    ILogger<KyrolusPaymobPaymentProvider>? logger = null) : IKyrolusPaymentProvider
{
    public string ProviderName => "Paymob";
    public IReadOnlyList<string> SupportedCurrencies => ["EGP", "USD", "AED", "SAR", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.KioskOrRetail,
        KyrolusPaymentMethodType.BuyNowPayLater,
        KyrolusPaymentMethodType.InstaPay
    ];

    private readonly KyrolusPaymobOptions _options = options.Value;

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var payload = new { api_key = _options.ApiKey };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/auth/tokens")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var authToken = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            var amountCents = (int)Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero);

            // 1. Register Order
            var orderPayload = new
            {
                auth_token = authToken,
                delivery_needed = "false",
                amount_cents = amountCents.ToString(),
                currency = request.Currency.ToUpperInvariant(),
                merchant_order_id = request.OrderId
            };

            var orderReq = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/ecommerce/orders")
            {
                Content = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json")
            };
            var orderRes = await httpClient.SendAsync(orderReq, cancellationToken).ConfigureAwait(false);
            var orderContent = await orderRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var orderDoc = JsonDocument.Parse(orderContent);
            var paymobOrderId = orderDoc.RootElement.GetProperty("id").GetInt64();

            // 2. Obtain Payment Key
            var customer = request.Customer;
            var keyPayload = new
            {
                auth_token = authToken,
                amount_cents = amountCents.ToString(),
                expiration = 3600,
                order_id = paymobOrderId.ToString(),
                billing_data = new
                {
                    first_name = customer?.Name?.Split(' ').FirstOrDefault() ?? "Guest",
                    last_name = customer?.Name?.Split(' ').Skip(1).FirstOrDefault() ?? "Customer",
                    email = customer?.Email ?? "customer@example.com",
                    phone_number = customer?.PhoneNumber ?? "+201000000000",
                    apartment = "NA",
                    floor = "NA",
                    street = customer?.AddressLine1 ?? "NA",
                    building = "NA",
                    shipping_method = "NA",
                    postal_code = customer?.PostalCode ?? "NA",
                    city = customer?.City ?? "Cairo",
                    country = customer?.CountryCode ?? "EG",
                    state = customer?.State ?? "Cairo"
                },
                currency = request.Currency.ToUpperInvariant(),
                integration_id = _options.IntegrationId
            };

            var keyReq = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/acceptance/payment_keys")
            {
                Content = new StringContent(JsonSerializer.Serialize(keyPayload), Encoding.UTF8, "application/json")
            };
            var keyRes = await httpClient.SendAsync(keyReq, cancellationToken).ConfigureAwait(false);
            var keyContent = await keyRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var keyDoc = JsonDocument.Parse(keyContent);
            var paymentToken = keyDoc.RootElement.GetProperty("token").GetString()!;

            var iframeUrl = $"https://accept.paymob.com/api/acceptance/iframes/{_options.IframeId}?payment_token={paymentToken}";

            return new KyrolusPaymentResult
            {
                TransactionId = request.OrderId,
                ProviderTransactionId = paymobOrderId.ToString(),
                Status = KyrolusPaymentStatus.RequiresAction,
                Amount = request.Amount,
                Currency = request.Currency,
                RedirectUrl = iframeUrl,
                RawDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["payment_token"] = paymentToken,
                    ["paymob_order_id"] = paymobOrderId.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Paymob CreatePayment error for Order {OrderId}", request.OrderId);
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
        return Task.FromResult(new KyrolusPaymentResult
        {
            TransactionId = transactionId,
            Status = KyrolusPaymentStatus.Succeeded
        });
    }

    public async Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var authToken = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/acceptance/transactions/{transactionId}");
            httpRequest.Headers.Add("Authorization", $"Bearer {authToken}");

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Failed };
            }

            using var doc = JsonDocument.Parse(content);
            var success = doc.RootElement.GetProperty("success").GetBoolean();

            return new KyrolusPaymentResult
            {
                TransactionId = transactionId,
                ProviderTransactionId = transactionId,
                Status = success ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Failed
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
            var authToken = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            var amountCents = request.Amount.HasValue ? (int)Math.Round(request.Amount.Value * 100) : 0;

            var payload = new
            {
                auth_token = authToken,
                transaction_id = request.TransactionId,
                amount_cents = amountCents
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/acceptance/void_refund/refund")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var id = doc.RootElement.GetProperty("id").GetInt64().ToString();
                return new KyrolusRefundResult
                {
                    RefundId = id,
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
