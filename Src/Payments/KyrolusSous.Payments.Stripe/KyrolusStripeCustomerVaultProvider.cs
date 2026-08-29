using System.Net.Http.Headers;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Stripe;

public sealed class KyrolusStripeCustomerVaultProvider(
    HttpClient httpClient,
    IOptions<KyrolusStripeOptions> options,
    ILogger<KyrolusStripeCustomerVaultProvider>? logger = null) : IKyrolusCustomerVaultProvider
{
    public string ProviderName => "Stripe";
    private readonly KyrolusStripeOptions _options = options.Value;

    public async Task<KyrolusVaultCustomer> CreateCustomerAsync(KyrolusPaymentCustomer customer, CancellationToken cancellationToken = default)
    {
        try
        {
            var formValues = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrEmpty(customer.Name)) formValues.Add(new("name", customer.Name));
            if (!string.IsNullOrEmpty(customer.Email)) formValues.Add(new("email", customer.Email));
            if (!string.IsNullOrEmpty(customer.PhoneNumber)) formValues.Add(new("phone", customer.PhoneNumber));

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/customers")
            {
                Content = new FormUrlEncodedContent(formValues)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var id = doc.RootElement.GetProperty("id").GetString()!;
                return new KyrolusVaultCustomer
                {
                    CustomerId = id,
                    Name = customer.Name,
                    Email = customer.Email,
                    PhoneNumber = customer.PhoneNumber
                };
            }

            return new KyrolusVaultCustomer { CustomerId = customer.CustomerId ?? Guid.NewGuid().ToString("N") };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create Stripe customer");
            return new KyrolusVaultCustomer { CustomerId = customer.CustomerId ?? Guid.NewGuid().ToString("N") };
        }
    }

    public async Task<KyrolusVaultResult> SavePaymentMethodAsync(KyrolusSavePaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("customer", request.CustomerId),
                new("payment_method_types[]", "card")
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/setup_intents")
            {
                Content = new FormUrlEncodedContent(formValues)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var id = root.GetProperty("id").GetString()!;
                var clientSecret = root.TryGetProperty("client_secret", out var cs) ? cs.GetString() : null;

                return new KyrolusVaultResult
                {
                    Succeeded = true,
                    CustomerId = request.CustomerId,
                    PaymentMethodId = id,
                    ClientSecretOrSetupUrl = clientSecret
                };
            }

            return new KyrolusVaultResult { Succeeded = false, ErrorMessage = content };
        }
        catch (Exception ex)
        {
            return new KyrolusVaultResult { Succeeded = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<IReadOnlyList<KyrolusSavedPaymentMethod>> ListPaymentMethodsAsync(string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/customers/{customerId}/payment_methods?type=card");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var list = new List<KyrolusSavedPaymentMethod>();
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetString()!;
                        string? last4 = null;
                        string? brand = null;
                        int? expMonth = null;
                        int? expYear = null;

                        if (item.TryGetProperty("card", out var card))
                        {
                            if (card.TryGetProperty("last4", out var l)) last4 = l.GetString();
                            if (card.TryGetProperty("brand", out var b)) brand = b.GetString();
                            if (card.TryGetProperty("exp_month", out var em)) expMonth = em.GetInt32();
                            if (card.TryGetProperty("exp_year", out var ey)) expYear = ey.GetInt32();
                        }

                        list.Add(new KyrolusSavedPaymentMethod
                        {
                            PaymentMethodId = id,
                            CustomerId = customerId,
                            MethodType = KyrolusPaymentMethodType.CreditCard,
                            LastFourDigits = last4,
                            CardBrand = brand,
                            ExpirationMonth = expMonth,
                            ExpirationYear = expYear
                        });
                    }
                }
                return list.AsReadOnly();
            }

            return [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> DeletePaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/payment_methods/{paymentMethodId}/detach");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetDefaultPaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/customers/{customerId}")
            {
                Content = new FormUrlEncodedContent([new("invoice_settings[default_payment_method]", paymentMethodId)])
            };
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
