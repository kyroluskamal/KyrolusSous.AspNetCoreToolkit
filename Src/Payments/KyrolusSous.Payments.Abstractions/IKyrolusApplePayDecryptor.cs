namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusApplePayDecryptor
{
    Task<KyrolusDecryptedPaymentTokenResult> DecryptTokenAsync(
        KyrolusApplePayPaymentToken token,
        CancellationToken cancellationToken = default);
}
