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

    public Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Succeeded });
    }

    public Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new KyrolusPaymentResult { TransactionId = transactionId, Status = KyrolusPaymentStatus.Succeeded });
    }

    public Task<KyrolusRefundResult> RefundPaymentAsync(KyrolusRefundRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new KyrolusRefundResult
        {
            RefundId = Guid.NewGuid().ToString("N"),
            TransactionId = request.TransactionId,
            Succeeded = true,
            RefundedAmount = request.Amount
        });
    }

    public Task<bool> CancelPaymentAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
