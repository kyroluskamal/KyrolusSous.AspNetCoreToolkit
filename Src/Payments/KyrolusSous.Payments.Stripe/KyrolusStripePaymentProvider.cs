using System.Net.Http.Headers;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Stripe;

public sealed class KyrolusStripePaymentProvider(
    HttpClient httpClient,
    IOptions<KyrolusStripeOptions> options,
    ILogger<KyrolusStripePaymentProvider>? logger = null) : IKyrolusPaymentProvider
{
    public string ProviderName => "Stripe";
    public IReadOnlyList<string> SupportedCurrencies => ["USD", "EUR", "GBP", "CAD", "AUD", "JPY", "CHF", "AED", "EGP", "*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => [
        KyrolusPaymentMethodType.CreditCard,
        KyrolusPaymentMethodType.DebitCard,
        KyrolusPaymentMethodType.DigitalWallet,
        KyrolusPaymentMethodType.DirectDebit,
        KyrolusPaymentMethodType.BuyNowPayLater
    ];

    private readonly KyrolusStripeOptions _options = options.Value;

    public async Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var amountInCents = (long)Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero);
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("amount", amountInCents.ToString()),
                new("currency", request.Currency.ToLowerInvariant()),
                new("payment_method_types[]", "card"),
                new("description", request.Description ?? $"Order {request.OrderId}"),
                new("metadata[order_id]", request.OrderId)
            };

            if (!string.IsNullOrEmpty(request.SuccessUrl))
            {
                formValues.Add(new("return_url", request.SuccessUrl));
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/payment_intents")
            {
                Content = new FormUrlEncodedContent(formValues)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger?.LogError("Stripe CreatePayment failed with status {Status}: {Error}", response.StatusCode, content);
                return new KyrolusPaymentResult
                {
                    TransactionId = request.OrderId,
                    Status = KyrolusPaymentStatus.Failed,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ErrorMessage = $"Stripe error: {response.StatusCode}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var intentId = root.GetProperty("id").GetString() ?? request.OrderId;
            var statusStr = root.GetProperty("status").GetString();
            var clientSecret = root.TryGetProperty("client_secret", out var cs) ? cs.GetString() : null;

            var status = statusStr switch
            {
                "succeeded" => KyrolusPaymentStatus.Succeeded,
                "requires_action" => KyrolusPaymentStatus.RequiresAction,
                "requires_payment_method" or "requires_confirmation" => KyrolusPaymentStatus.Pending,
                _ => KyrolusPaymentStatus.Processing
            };

            var details = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (clientSecret != null) details["client_secret"] = clientSecret;

            return new KyrolusPaymentResult
            {
                TransactionId = request.OrderId,
                ProviderTransactionId = intentId,
                Status = status,
                Amount = request.Amount,
                Currency = request.Currency,
                RedirectUrl = clientSecret != null ? $"https://checkout.stripe.com/c/pay/{intentId}" : null,
                RawDetails = details
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Exception creating Stripe payment for Order {OrderId}", request.OrderId);
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
            var formValues = new List<KeyValuePair<string, string>>();
            if (amount.HasValue)
            {
                var cents = (long)Math.Round(amount.Value * 100, MidpointRounding.AwayFromZero);
                formValues.Add(new("amount_to_capture", cents.ToString()));
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/payment_intents/{transactionId}/capture")
            {
                Content = new FormUrlEncodedContent(formValues)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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
            return new KyrolusPaymentResult
            {
                TransactionId = transactionId,
                Status = KyrolusPaymentStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/payment_intents/{transactionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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
                "succeeded" => KyrolusPaymentStatus.Succeeded,
                "canceled" => KyrolusPaymentStatus.Cancelled,
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
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("payment_intent", request.TransactionId)
            };
            if (request.Amount.HasValue)
            {
                var cents = (long)Math.Round(request.Amount.Value * 100, MidpointRounding.AwayFromZero);
                formValues.Add(new("amount", cents.ToString()));
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/refunds")
            {
                Content = new FormUrlEncodedContent(formValues)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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
            return new KyrolusRefundResult
            {
                RefundId = string.Empty,
                TransactionId = request.TransactionId,
                Succeeded = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> CancelPaymentAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/payment_intents/{transactionId}/cancel");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
