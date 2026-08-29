using System.Security.Cryptography;
using System.Text;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultNetworkTokenizationEngine : IKyrolusNetworkTokenizationEngine
{
    public Task<KyrolusNetworkTokenResult> TokenizeCardAsync(
        KyrolusTokenizePanRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenRef = $"tok_net_{Guid.NewGuid():N}";
        var last4 = request.PrimaryAccountNumber.Length >= 4
            ? request.PrimaryAccountNumber[^4..]
            : "0000";
        var tokenPan = $"489999{Random.Shared.Next(100000, 999999)}{last4}";

        return Task.FromResult(new KyrolusNetworkTokenResult
        {
            NetworkTokenNumber = tokenPan,
            TokenReferenceId = tokenRef,
            Cryptogram = Convert.ToBase64String(RandomNumberGenerator.GetBytes(20)),
            EciFlag = "05",
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear + 5, // Network tokens extend past physical card expiry
            IsActive = true
        });
    }

    public Task<string> GenerateCryptogramForPaymentAsync(
        string tokenReferenceId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var raw = $"{tokenReferenceId}:{amount}:{currency}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Task.FromResult(Convert.ToBase64String(hash));
    }
}
