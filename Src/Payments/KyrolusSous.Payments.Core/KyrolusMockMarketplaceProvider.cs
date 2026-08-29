using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockMarketplaceProvider : IKyrolusMarketplaceProvider
{
    public string ProviderName => "Mock";
    private readonly ConcurrentDictionary<string, KyrolusMerchantAccountResult> _accounts = new();
    private readonly ConcurrentDictionary<string, KyrolusSplitTransferResult> _transfers = new();

    public Task<KyrolusMerchantAccountResult> CreateConnectedAccountAsync(KyrolusMerchantAccountRequest request, CancellationToken cancellationToken = default)
    {
        var accId = $"acct_{Guid.NewGuid():N}";
        var result = new KyrolusMerchantAccountResult
        {
            AccountId = accId,
            OnboardingUrl = $"https://connect.kyrolus.test/setup/{accId}",
            IsChargesEnabled = true,
            IsPayoutsEnabled = true
        };

        _accounts[accId] = result;
        return Task.FromResult(result);
    }

    public Task<KyrolusSplitTransferResult> TransferToConnectedAccountAsync(KyrolusSplitTransferRequest request, CancellationToken cancellationToken = default)
    {
        var transferId = $"tr_{Guid.NewGuid():N}";
        var fee = request.PlatformFeeAmount ?? (request.Amount * 0.05m); // 5% default
        var netTransfer = request.Amount - fee;

        var result = new KyrolusSplitTransferResult
        {
            TransferId = transferId,
            DestinationAccountId = request.DestinationAccountId,
            Amount = netTransfer,
            PlatformFeeAmount = fee,
            Currency = request.Currency,
            Succeeded = true
        };

        _transfers[transferId] = result;
        return Task.FromResult(result);
    }
}
