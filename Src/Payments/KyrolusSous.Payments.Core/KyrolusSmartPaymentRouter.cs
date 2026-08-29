using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusSmartPaymentRouter(
    IKyrolusPaymentFactory paymentFactory,
    ILogger<KyrolusSmartPaymentRouter>? logger = null) : IKyrolusSmartPaymentRouter
{
    public IKyrolusPaymentProvider ResolveBestProvider(KyrolusPaymentRequest request)
    {
        var currencyProvider = paymentFactory.GetProviderForCurrency(request.Currency);
        if (currencyProvider is not null)
        {
            return currencyProvider;
        }

        var all = paymentFactory.GetAllProviders();
        if (all.Count > 0)
        {
            return all[0];
        }

        throw new InvalidOperationException("No payment providers registered in the system.");
    }

    public async Task<KyrolusPaymentResult> ExecuteWithFailoverAsync(
        KyrolusPaymentRequest request,
        IReadOnlyList<string>? preferredProviderOrder = null,
        CancellationToken cancellationToken = default)
    {
        var providersToTry = new List<IKyrolusPaymentProvider>();

        if (preferredProviderOrder is { Count: > 0 })
        {
            foreach (var name in preferredProviderOrder)
            {
                try
                {
                    var p = paymentFactory.GetProvider(name);
                    if (p is not null && !providersToTry.Contains(p))
                    {
                        providersToTry.Add(p);
                    }
                }
                catch
                {
                    // Provider not found, continue
                }
            }
        }

        // Add default currency provider if not already added
        var best = paymentFactory.GetProviderForCurrency(request.Currency);
        if (best is not null && !providersToTry.Contains(best))
        {
            providersToTry.Add(best);
        }

        // Add remaining providers as last resort
        foreach (var p in paymentFactory.GetAllProviders())
        {
            if (!providersToTry.Contains(p))
            {
                providersToTry.Add(p);
            }
        }

        if (providersToTry.Count == 0)
        {
            throw new InvalidOperationException("No suitable payment providers available for processing.");
        }

        var errors = new List<string>();

        foreach (var provider in providersToTry)
        {
            try
            {
                logger?.LogInformation("Attempting payment for Order {OrderId} using provider {Provider}", request.OrderId, provider.ProviderName);
                var result = await provider.CreatePaymentAsync(request, cancellationToken).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return result;
                }

                logger?.LogWarning("Provider {Provider} failed for Order {OrderId}: {Error}", provider.ProviderName, request.OrderId, result.ErrorMessage);
                errors.Add($"{provider.ProviderName}: {result.ErrorMessage}");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Provider {Provider} threw an exception for Order {OrderId}", provider.ProviderName, request.OrderId);
                errors.Add($"{provider.ProviderName}: {ex.Message}");
            }
        }

        return new KyrolusPaymentResult
        {
            TransactionId = request.OrderId,
            Status = KyrolusPaymentStatus.Failed,
            Amount = request.Amount,
            Currency = request.Currency,
            ErrorMessage = $"All payment providers failed: {string.Join(" | ", errors)}"
        };
    }
}
