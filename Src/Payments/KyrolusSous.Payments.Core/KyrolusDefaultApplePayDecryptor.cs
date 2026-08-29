using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultApplePayDecryptor : IKyrolusApplePayDecryptor
{
    public Task<KyrolusDecryptedPaymentTokenResult> DecryptTokenAsync(
        KyrolusApplePayPaymentToken token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token.PaymentData))
        {
            return Task.FromResult(new KyrolusDecryptedPaymentTokenResult
            {
                Succeeded = false,
                PrimaryAccountNumber = string.Empty,
                ExpirationMonth = 0,
                ExpirationYear = 0,
                ErrorMessage = "Payment data cannot be empty."
            });
        }

        // Simulates EC_v1 token validation & extraction of the DPAN
        var dpan = "4242424242424242";
        return Task.FromResult(new KyrolusDecryptedPaymentTokenResult
        {
            Succeeded = true,
            PrimaryAccountNumber = dpan,
            ExpirationMonth = 12,
            ExpirationYear = 2030,
            CardholderName = token.DisplayName ?? "Apple Pay User",
            PaymentDataType = "3DSecure"
        });
    }
}
