using System.Net.Http.Headers;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Stripe;

public sealed class KyrolusStripeSubscriptionProvider(
    HttpClient httpClient,
    IOptions<KyrolusStripeOptions> options,
    ILogger<KyrolusStripeSubscriptionProvider>? logger = null) : IKyrolusSubscriptionProvider
{
    public string ProviderName => "Stripe";
    private readonly KyrolusStripeOptions _options = options.Value;

    public async Task<KyrolusSubscriptionPlan> CreatePlanAsync(KyrolusSubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        try
        {
            var amountCents = (long)Math.Round(plan.Amount * 100, MidpointRounding.AwayFromZero);
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("nickname", plan.Name),
                new("unit_amount", amountCents.ToString()),
                new("currency", plan.Currency.ToLowerInvariant()),
                new("recurring[interval]", plan.Interval.ToString().ToLowerInvariant()),
                new("recurring[interval_count]", plan.IntervalCount.ToString()),
                new("product_data[name]", plan.Name)
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/prices")
            {
                Content = new FormUrlEncodedContent(formValues)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var priceId = doc.RootElement.GetProperty("id").GetString()!;
                return plan with { PlanId = priceId };
            }

            logger?.LogError("Failed to create Stripe plan: {Content}", content);
            return plan;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Exception creating Stripe subscription plan");
            return plan;
        }
    }

    public async Task<KyrolusSubscriptionResult> CreateSubscriptionAsync(KyrolusSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("customer", request.CustomerId),
                new("items[0][price]", request.PlanId),
                new("payment_behavior", "default_incomplete")
            };

            if (!string.IsNullOrEmpty(request.PaymentMethodId))
            {
                formValues.Add(new("default_payment_method", request.PaymentMethodId));
            }

            if (request.CustomTrialDays.HasValue)
            {
                formValues.Add(new("trial_period_days", request.CustomTrialDays.Value.ToString()));
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/subscriptions")
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
                var subId = root.GetProperty("id").GetString()!;
                var statusStr = root.GetProperty("status").GetString()!;

                string? clientSecret = null;
                if (root.TryGetProperty("latest_invoice", out var invoice) &&
                    invoice.ValueKind == JsonValueKind.Object &&
                    invoice.TryGetProperty("payment_intent", out var pi) &&
                    pi.ValueKind == JsonValueKind.Object &&
                    pi.TryGetProperty("client_secret", out var cs))
                {
                    clientSecret = cs.GetString();
                }

                return new KyrolusSubscriptionResult
                {
                    SubscriptionId = subId,
                    CustomerId = request.CustomerId,
                    PlanId = request.PlanId,
                    Status = statusStr == "active" ? KyrolusSubscriptionStatus.Active : KyrolusSubscriptionStatus.Trailing,
                    ClientSecret = clientSecret
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
            return new KyrolusSubscriptionResult
            {
                SubscriptionId = string.Empty,
                Status = KyrolusSubscriptionStatus.Incomplete,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<KyrolusSubscriptionResult> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/subscriptions/{subscriptionId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var statusStr = root.GetProperty("status").GetString();
                var cancelAtEnd = root.TryGetProperty("cancel_at_period_end", out var c) && c.GetBoolean();

                var status = statusStr switch
                {
                    "active" => KyrolusSubscriptionStatus.Active,
                    "trialing" => KyrolusSubscriptionStatus.Trailing,
                    "canceled" => KyrolusSubscriptionStatus.Cancelled,
                    "past_due" => KyrolusSubscriptionStatus.PastDue,
                    "paused" => KyrolusSubscriptionStatus.Paused,
                    _ => KyrolusSubscriptionStatus.Incomplete
                };

                return new KyrolusSubscriptionResult
                {
                    SubscriptionId = subscriptionId,
                    Status = status,
                    CancelAtPeriodEnd = cancelAtEnd
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
            var url = cancelImmediately
                ? $"{_options.BaseUrl.TrimEnd('/')}/subscriptions/{subscriptionId}"
                : $"{_options.BaseUrl.TrimEnd('/')}/subscriptions/{subscriptionId}";

            var httpRequest = cancelImmediately
                ? new HttpRequestMessage(HttpMethod.Delete, url)
                : new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent([new("cancel_at_period_end", "true")])
                };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return new KyrolusSubscriptionResult
            {
                SubscriptionId = subscriptionId,
                Status = response.IsSuccessStatusCode ? KyrolusSubscriptionStatus.Cancelled : KyrolusSubscriptionStatus.Active,
                CancelAtPeriodEnd = !cancelImmediately
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/subscriptions/{subscriptionId}")
            {
                Content = new FormUrlEncodedContent([new("pause_collection[behavior]", "void")])
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/subscriptions/{subscriptionId}")
            {
                Content = new FormUrlEncodedContent([new("pause_collection", "")])
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = response.IsSuccessStatusCode ? KyrolusSubscriptionStatus.Active : KyrolusSubscriptionStatus.Paused };
        }
        catch
        {
            return new KyrolusSubscriptionResult { SubscriptionId = subscriptionId, Status = KyrolusSubscriptionStatus.Cancelled };
        }
    }
}
