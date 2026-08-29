using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockPaymentProvider : IKyrolusPaymentProvider
{
    public string ProviderName => "Mock";
    public IReadOnlyList<string> SupportedCurrencies => ["*"];
    public IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods => Enum.GetValues<KyrolusPaymentMethodType>();

    private readonly ConcurrentDictionary<string, KyrolusPaymentResult> _payments = new();

    public Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var txId = $"mock_tx_{Guid.NewGuid():N}";
        var result = new KyrolusPaymentResult
        {
            TransactionId = txId,
            ProviderTransactionId = $"mock_p_{Guid.NewGuid():N}",
            Status = KyrolusPaymentStatus.Succeeded,
            Amount = request.Amount,
            Currency = request.Currency,
            RedirectUrl = request.SuccessUrl ?? $"https://checkout.mock.local/pay/{txId}",
            ReferenceCode = $"MOCK-{Random.Shared.Next(100000, 999999)}"
        };

        _payments[txId] = result;
        return Task.FromResult(result);
    }

    public Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        if (_payments.TryGetValue(transactionId, out var existing))
        {
            var updated = existing with { Status = KyrolusPaymentStatus.Succeeded };
            _payments[transactionId] = updated;
            return Task.FromResult(updated);
        }

        return Task.FromResult(new KyrolusPaymentResult
        {
            TransactionId = transactionId,
            Status = KyrolusPaymentStatus.Failed,
            ErrorMessage = "Transaction not found."
        });
    }

    public Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        if (_payments.TryGetValue(transactionId, out var existing))
        {
            return Task.FromResult(existing);
        }

        return Task.FromResult(new KyrolusPaymentResult
        {
            TransactionId = transactionId,
            Status = KyrolusPaymentStatus.Failed,
            ErrorMessage = "Transaction not found."
        });
    }

    public Task<KyrolusRefundResult> RefundPaymentAsync(KyrolusRefundRequest request, CancellationToken cancellationToken = default)
    {
        if (_payments.TryGetValue(request.TransactionId, out var existing))
        {
            _payments[request.TransactionId] = existing with { Status = KyrolusPaymentStatus.Refunded };
            return Task.FromResult(new KyrolusRefundResult
            {
                RefundId = $"mock_ref_{Guid.NewGuid():N}",
                TransactionId = request.TransactionId,
                Succeeded = true,
                RefundedAmount = request.Amount ?? existing.Amount
            });
        }

        return Task.FromResult(new KyrolusRefundResult
        {
            RefundId = $"mock_ref_{Guid.NewGuid():N}",
            TransactionId = request.TransactionId,
            Succeeded = false,
            ErrorMessage = "Original transaction not found."
        });
    }

    public Task<bool> CancelPaymentAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        if (_payments.TryGetValue(transactionId, out var existing))
        {
            _payments[transactionId] = existing with { Status = KyrolusPaymentStatus.Cancelled };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
