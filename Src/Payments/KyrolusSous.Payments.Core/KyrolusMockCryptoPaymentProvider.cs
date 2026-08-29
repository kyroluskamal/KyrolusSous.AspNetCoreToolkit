using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockCryptoPaymentProvider : IKyrolusCryptoPaymentProvider
{
    public string ProviderName => "MockCrypto";
    private readonly ConcurrentDictionary<string, KyrolusCryptoPaymentResult> _intents = new();

    public Task<KyrolusCryptoPaymentResult> CreatePaymentIntentAsync(
        KyrolusCreateCryptoPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = $"crypto_{Guid.NewGuid():N}";
        var address = request.Network switch
        {
            KyrolusCryptoNetwork.Tron_TRC20 => "TX1234567890abcdefTRONmockAddress",
            KyrolusCryptoNetwork.Ethereum_ERC20 => "0x1234567890abcdef1234567890abcdef12345678",
            KyrolusCryptoNetwork.Solana => "So11111111111111111111111111111111111111112",
            _ => "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"
        };

        var cryptoAmount = request.CryptoCurrency.ToUpperInvariant() switch
        {
            "USDT" or "USDC" => request.FiatAmount,
            "BTC" => Math.Round(request.FiatAmount / 65000m, 6),
            "ETH" => Math.Round(request.FiatAmount / 3500m, 6),
            _ => request.FiatAmount
        };

        var result = new KyrolusCryptoPaymentResult
        {
            PaymentId = id,
            OrderId = request.OrderId,
            DepositAddress = address,
            RequiredCryptoAmount = cryptoAmount,
            CryptoCurrency = request.CryptoCurrency.ToUpperInvariant(),
            Network = request.Network,
            Status = KyrolusCryptoPaymentStatus.AwaitingDeposit,
            QrCodePayload = $"{request.CryptoCurrency.ToLowerInvariant()}:{address}?amount={cryptoAmount}",
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(request.ExpiresIn ?? TimeSpan.FromMinutes(30))
        };

        _intents[id] = result;
        return Task.FromResult(result);
    }

    public Task<KyrolusCryptoPaymentResult> CheckPaymentStatusAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        if (_intents.TryGetValue(paymentId, out var intent))
        {
            return Task.FromResult(intent);
        }

        return Task.FromResult(new KyrolusCryptoPaymentResult
        {
            PaymentId = paymentId,
            OrderId = string.Empty,
            DepositAddress = string.Empty,
            RequiredCryptoAmount = 0m,
            CryptoCurrency = "USDT",
            Network = KyrolusCryptoNetwork.Tron_TRC20,
            Status = KyrolusCryptoPaymentStatus.Expired,
            ExpiresAtUtc = DateTimeOffset.UtcNow
        });
    }
}
