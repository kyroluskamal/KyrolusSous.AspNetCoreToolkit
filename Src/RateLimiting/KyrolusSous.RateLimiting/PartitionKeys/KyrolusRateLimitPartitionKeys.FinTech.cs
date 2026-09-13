using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.RateLimiting;

/// <summary>
/// FinTech, Payment Processors, Card Networks (Anti-Carding), Transaction Value Tiers, and Open Banking Clearing Rails partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    private static readonly string[] DefaultProcessorHeaders =
    [
        "X-Payment-Processor",
        "X-Processor",
        "X-Payment-Gateway",
        "X-PSP"
    ];

    /// <summary>
    /// Resolves a partition key based on the targeted Payment Service Provider or Payment Processor (<c>"fin_proc_stripe"</c>, <c>"fin_proc_adyen"</c>, <c>"fin_proc_paypal"</c>).
    /// Prevents downtime or rate-limit throttling from one upstream payment gateway from impacting other checkout routes.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="processorHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The payment processor partition key formatted as <c>"fin_proc_{name}"</c>, or <c>"fin_proc_unknown"</c>.</returns>
    public static string GetPaymentProcessorKey(HttpContext context, string[]? processorHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = processorHeaderCandidates is { Length: > 0 } ? processorHeaderCandidates : DefaultProcessorHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"fin_proc_{SanitizeSegment(raw)}";
            }
        }

        return "fin_proc_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the payment processor with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="processorHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{processorKey}:{clientKey}"</c>.</returns>
    public static string GetPaymentProcessorAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? processorHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var procKey = GetPaymentProcessorKey(context, processorHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{procKey}:{clientKey}";
    }

    private static readonly string[] DefaultAmountHeaders =
    [
        "X-Transaction-Amount",
        "X-Amount",
        "X-Payment-Amount"
    ];

    /// <summary>
    /// Resolves a partition key based on transaction value tiers (<c>"fin_amount_micropayment"</c>, <c>"fin_amount_standard"</c>, <c>"fin_amount_high_value"</c>).
    /// Guarantees high-value commercial wire transfers receive dedicated throughput while heavily throttling suspicious micro-transactions.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="micropaymentThreshold">Threshold up to which a transaction is categorized as a micropayment. Defaults to 5.</param>
    /// <param name="highValueThreshold">Threshold above which a transaction is categorized as high value. Defaults to 10,000.</param>
    /// <param name="amountHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The transaction amount tier key.</returns>
    public static string GetTransactionAmountTierKey(
        HttpContext context,
        decimal micropaymentThreshold = 5m,
        decimal highValueThreshold = 10000m,
        string[]? amountHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(micropaymentThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(highValueThreshold, micropaymentThreshold);

        var candidates = amountHeaderCandidates is { Length: > 0 } ? amountHeaderCandidates : DefaultAmountHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                if (decimal.TryParse(val.ToString().Trim(), CultureInfo.InvariantCulture, out var amount))
                {
                    if (amount <= micropaymentThreshold)
                        return "fin_amount_micropayment";
                    if (amount >= highValueThreshold)
                        return "fin_amount_high_value";
                    return "fin_amount_standard";
                }
            }
        }

        return "fin_amount_unspecified";
    }

    /// <summary>
    /// Resolves a compound partition key combining the transaction amount tier with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="micropaymentThreshold">Threshold for micropayments.</param>
    /// <param name="highValueThreshold">Threshold for high value transactions.</param>
    /// <param name="amountHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{amountTierKey}:{clientKey}"</c>.</returns>
    public static string GetTransactionAmountTierAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        decimal micropaymentThreshold = 5m,
        decimal highValueThreshold = 10000m,
        string[]? amountHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var amountKey = GetTransactionAmountTierKey(context, micropaymentThreshold, highValueThreshold, amountHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{amountKey}:{clientKey}";
    }

    private static readonly string[] DefaultCardHeaders =
    [
        "X-Card-Network",
        "X-Card-Brand",
        "X-Payment-Scheme",
        "X-Card-Scheme"
    ];

    /// <summary>
    /// Resolves a partition key based on payment card networks (<c>"card_visa"</c>, <c>"card_mastercard"</c>, <c>"card_amex"</c>, <c>"card_mada"</c>, <c>"card_meeza"</c>).
    /// Defends against automated Carding Attacks (testing stolen PAN credentials) by isolating velocity limits per card network brand.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="cardHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The card network partition key formatted as <c>"card_{brand}"</c>, or <c>"card_unknown"</c>.</returns>
    public static string GetCardNetworkKey(HttpContext context, string[]? cardHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = cardHeaderCandidates is { Length: > 0 } ? cardHeaderCandidates : DefaultCardHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (raw.Contains("visa")) return "card_visa";
                if (raw.Contains("mastercard") || raw.Contains("master_card") || raw.Contains("mc")) return "card_mastercard";
                if (raw.Contains("amex") || raw.Contains("american_express")) return "card_amex";
                if (raw.Contains("discover")) return "card_discover";
                if (raw.Contains("unionpay") || raw.Contains("cup")) return "card_unionpay";
                if (raw.Contains("jcb")) return "card_jcb";
                if (raw.Contains("mada")) return "card_mada";
                if (raw.Contains("meeza")) return "card_meeza";

                return $"card_{SanitizeSegment(raw)}";
            }
        }

        return "card_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the card network brand with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="cardHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{cardNetworkKey}:{clientKey}"</c>.</returns>
    public static string GetCardNetworkAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? cardHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cardKey = GetCardNetworkKey(context, cardHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{cardKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the payment clearing rail or Open Banking protocol (<c>"fin_rail_instant"</c>, <c>"fin_rail_batch"</c>, <c>"fin_rail_standard"</c>).
    /// Segregates real-time rails (SEPA Instant, FedNow, FasterPayments, PIX) from asynchronous settlement rails (ACH, BACS).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The payment rail partition key.</returns>
    public static string GetPaymentRailKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-Payment-Rail", out var val) ||
            context.Request.Headers.TryGetValue("X-Clearing-System", out val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (raw.Contains("instant") || raw.Contains("fednow") || raw.Contains("faster") || raw.Contains("pix") || raw.Contains("sepa_instant"))
                return "fin_rail_instant";
            if (raw.Contains("ach") || raw.Contains("bacs") || raw.Contains("batch"))
                return "fin_rail_batch";

            return $"fin_rail_{SanitizeSegment(raw)}";
        }

        return "fin_rail_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the payment clearing rail with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{railKey}:{clientKey}"</c>.</returns>
    public static string GetPaymentRailAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var railKey = GetPaymentRailKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{railKey}:{clientKey}";
    }
}
