using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultApplePayDecryptor : IKyrolusApplePayDecryptor
{
    public Task<KyrolusDecryptedPaymentTokenResult> DecryptTokenAsync(
        KyrolusApplePayPaymentToken token,
        CancellationToken cancellationToken = default)
    {
        // No merchant identity certificate / private key is configured anywhere in this library,
        // so the EC_v1/RSA_v1 payment data cannot actually be decrypted here. Returning a fake
        // PAN would silently charge the wrong card instead of failing loudly.
        return Task.FromResult(new KyrolusDecryptedPaymentTokenResult
        {
            Succeeded = false,
            PrimaryAccountNumber = string.Empty,
            ExpirationMonth = 0,
            ExpirationYear = 0,
            ErrorMessage = "Apple Pay token decryption is not implemented. Register a real IKyrolusApplePayDecryptor " +
                            "backed by your merchant identity certificate before accepting Apple Pay payments."
        });
    }
}
