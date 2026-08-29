using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultSplitTenderProvider(
    IKyrolusPaymentFactory paymentFactory,
    ILogger<KyrolusDefaultSplitTenderProvider>? logger = null) : IKyrolusSplitTenderProvider
{
    public async Task<KyrolusSplitTenderResult> ExecuteSplitTenderAsync(
        KyrolusSplitTenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var totalLegs = request.Legs.Sum(l => l.Amount);
        if (totalLegs != request.TotalAmount)
        {
            return new KyrolusSplitTenderResult
            {
                OrderId = request.OrderId,
                Succeeded = false,
                TotalAmount = request.TotalAmount,
                Currency = request.Currency,
                ErrorMessage = $"Sum of tender legs ({totalLegs}) does not equal total amount ({request.TotalAmount})."
            };
        }

        var results = new List<KyrolusTenderLegResult>();
        var executedProviders = new List<(IKyrolusPaymentProvider Provider, string TxId, decimal Amount)>();

        foreach (var leg in request.Legs)
        {
            try
            {
                var provider = paymentFactory.GetProvider(leg.ProviderName);
                var paymentReq = new KyrolusPaymentRequest
                {
                    OrderId = $"{request.OrderId}_{leg.ProviderName}",
                    Amount = leg.Amount,
                    Currency = request.Currency,
                    Description = request.Description
                };

                var res = await provider.CreatePaymentAsync(paymentReq, cancellationToken).ConfigureAwait(false);
                if (res.IsSuccess)
                {
                    results.Add(new KyrolusTenderLegResult
                    {
                        ProviderName = leg.ProviderName,
                        Amount = leg.Amount,
                        Succeeded = true,
                        TransactionId = res.TransactionId
                    });
                    executedProviders.Add((provider, res.TransactionId, leg.Amount));
                }
                else
                {
                    // Leg failed -> Rollback previous legs
                    await RollbackLegsAsync(executedProviders, request.Currency, cancellationToken).ConfigureAwait(false);

                    results.Add(new KyrolusTenderLegResult
                    {
                        ProviderName = leg.ProviderName,
                        Amount = leg.Amount,
                        Succeeded = false,
                        ErrorMessage = res.ErrorMessage
                    });

                    return new KyrolusSplitTenderResult
                    {
                        OrderId = request.OrderId,
                        Succeeded = false,
                        TotalAmount = request.TotalAmount,
                        Currency = request.Currency,
                        LegResults = results.AsReadOnly(),
                        ErrorMessage = $"Tender leg with provider '{leg.ProviderName}' failed: {res.ErrorMessage}. Previous legs were rolled back."
                    };
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Split tender leg {Provider} failed for Order {OrderId}", leg.ProviderName, request.OrderId);
                await RollbackLegsAsync(executedProviders, request.Currency, cancellationToken).ConfigureAwait(false);

                results.Add(new KyrolusTenderLegResult
                {
                    ProviderName = leg.ProviderName,
                    Amount = leg.Amount,
                    Succeeded = false,
                    ErrorMessage = ex.Message
                });

                return new KyrolusSplitTenderResult
                {
                    OrderId = request.OrderId,
                    Succeeded = false,
                    TotalAmount = request.TotalAmount,
                    Currency = request.Currency,
                    LegResults = results.AsReadOnly(),
                    ErrorMessage = $"Tender leg with provider '{leg.ProviderName}' threw exception: {ex.Message}."
                };
            }
        }

        return new KyrolusSplitTenderResult
        {
            OrderId = request.OrderId,
            Succeeded = true,
            TotalAmount = request.TotalAmount,
            Currency = request.Currency,
            LegResults = results.AsReadOnly()
        };
    }

    private async Task RollbackLegsAsync(
        List<(IKyrolusPaymentProvider Provider, string TxId, decimal Amount)> executed,
        string currency,
        CancellationToken cancellationToken)
    {
        foreach (var (provider, txId, amount) in executed)
        {
            try
            {
                await provider.RefundPaymentAsync(new KyrolusRefundRequest
                {
                    TransactionId = txId,
                    Amount = amount,
                    Currency = currency,
                    Reason = "Split tender rollback due to partial failure"
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to rollback tender leg {TxId} on provider {Provider}", txId, provider.ProviderName);
            }
        }
    }
}
