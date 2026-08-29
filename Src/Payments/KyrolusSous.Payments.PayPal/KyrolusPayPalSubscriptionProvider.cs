using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusPayPalSubscriptionProvider(
    HttpClient httpClient,
    IOptions<KyrolusPayPalOptions> options,
    IKyrolusCacheProvider? cacheProvider = null,
    ILogger<KyrolusPayPalSubscriptionProvider>? logger = null) : IKyrolusSubscriptionProvider
{
    public string ProviderName => "PayPal";
    private readonly KyrolusPayPalOptions _options = options.Value;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var cacheKey = $"kyrolus:paypal:token:{_options.ClientId}";
        if (cacheProvider is not null)
        {
            var cached = await cacheProvider.GetAsync<string>(cacheKey, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(cached)) return cached;
        }

        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        var authBytes = Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}");
        var authHeader = Convert.ToBase64String(authBytes);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")])
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(content);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

        if (cacheProvider is not null)
        {
            await cacheProvider.SetAsync(cacheKey, _accessToken, TimeSpan.FromSeconds(Math.Max(10, expiresIn - 60)), cancellationToken).ConfigureAwait(false);
        }

        return _accessToken;
    }

    public async Task<KyrolusSubscriptionPlan> CreatePlanAsync(KyrolusSubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var payload = new
            {
                product_id = "PROD-GENERAL",
                name = plan.Name,
                description = plan.Description ?? plan.Name,
                status = "ACTIVE",
                billing_cycles = new[]
                {
                    new
                    {
                        frequency = new
                        {
                            interval_unit = plan.Interval.ToString().ToUpperInvariant(),
                            interval_count = plan.IntervalCount
                        },
                        tenure_type = "REGULAR",
                        sequence = 1,
                        total_cycles = 0,
                        pricing_scheme = new
                        {
                            fixed_price = new
                            {
                                value = plan.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                                currency_code = plan.Currency.ToUpperInvariant()
                            }
                        }
                    }
                },
                payment_preferences = new
                {
                    auto_bill_outstanding = true,
                    setup_fee_failure_action = "CONTINUE",
                    payment_failure_threshold = 3
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/billing/plans")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var planId = doc.RootElement.GetProperty("id").GetString()!;
                return plan with { PlanId = planId };
            }

            return plan;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create PayPal plan");
            return plan;
        }
    }

    public async Task<KyrolusSubscriptionResult> CreateSubscriptionAsync(KyrolusSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var payload = new
            {
                plan_id = request.PlanId,
                subscriber = new
                {
                    name = new { given_name = "Subscriber" },
                    email_address = request.CustomerId
                },
                application_context = new
                {
                    brand_name = "Merchant",
                    locale = "en-US",
                    shipping_preference = "NO_SHIPPING",
                    user_action = "SUBSCRIBE_NOW"
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/billing/subscriptions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var subId = root.GetProperty("id").GetString()!;
                var statusStr = root.GetProperty("status").GetString()!;

                return new KyrolusSubscriptionResult
                {
                    SubscriptionId = subId,
                    CustomerId = request.CustomerId,
                    PlanId = request.PlanId,
                    Status = statusStr == "ACTIVE" ? KyrolusSubscriptionStatus.Active : KyrolusSubscriptionStatus.Trailing
                };
            }

            return new KyrolusSubscriptionResult
            {
                SubscriptionId = string.Empty,
                Status = KyrolusSubscriptionStatus.Incomplete,
                ErrorMessage = content
            };
        }
        catch (Exception ex)
        {
            return new KyrolusSubscriptionResult { SubscriptionId = string.Empty, Status = KyrolusSubscriptionStatus.Incomplete, ErrorMessage = ex.Message };
        }
    }

    public async Task<KyrolusSubscriptionResult> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/v1/billing/subscriptions/{subscriptionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var statusStr = doc.RootElement.GetProperty("status").GetString();
                var status = statusStr switch
                {
                    "ACTIVE" => KyrolusSubscriptionStatus.Active,
                    "SUSPENDED" => KyrolusSubscriptionStatus.Paused,
                    "CANCELLED" => KyrolusSubscriptionStatus.Cancelled,
                    _ => KyrolusSubscriptionStatus.Incomplete
                };

                return new KyrolusSubscriptionResult
                {
                    SubscriptionId = subscriptionId,
                    Status = status
                };
            }

            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = KyrolusSubscriptionStatus.Cancelled };
        }
        catch (Exception ex)
        {
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = KyrolusSubscriptionStatus.Cancelled, ErrorMessage = ex.Message };
        }
    }

    public async Task<KyrolusSubscriptionResult> CancelSubscriptionAsync(string subscriptionId, bool cancelImmediately = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var payload = new { reason = "Customer requested cancellation" };
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/billing/subscriptions/{subscriptionId}/cancel")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return new KyrolusSubscriptionResult
            {
                SubscriptionId = subscriptionId,
                Status = response.IsSuccessStatusCode ? KyrolusSubscriptionStatus.Cancelled : KyrolusSubscriptionStatus.Active
            };
        }
        catch (Exception ex)
        {
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = KyrolusSubscriptionStatus.Cancelled, ErrorMessage = ex.Message };
        }
    }

    public async Task<KyrolusSubscriptionResult> PauseSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var payload = new { reason = "Customer paused subscription" };
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/billing/subscriptions/{subscriptionId}/suspend")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = response.IsSuccessStatusCode ? KyrolusSubscriptionStatus.Paused : KyrolusSubscriptionStatus.Active };
        }
        catch
        {
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = KyrolusSubscriptionStatus.Cancelled };
        }
    }

    public async Task<KyrolusSubscriptionResult> ResumeSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var payload = new { reason = "Customer resumed subscription" };
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/billing/subscriptions/{subscriptionId}/activate")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = response.IsSuccessStatusCode ? KyrolusSubscriptionStatus.Active : KyrolusSubscriptionStatus.Paused };
        }
        catch
        {
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = KyrolusSubscriptionStatus.Cancelled };
        }
    }
}
