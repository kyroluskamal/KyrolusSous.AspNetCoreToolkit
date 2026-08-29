namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusApplePayPaymentToken
{
    public required string PaymentData { get; init; } // Base64 Encrypted Payload
    public required string EphemeralPublicKey { get; init; }
    public required string PublicKeyHash { get; init; }
    public required string TransactionId { get; init; }
    public string? DisplayName { get; init; }
    public string? Network { get; init; } // Visa, Mastercard, etc.
}

public sealed record KyrolusDecryptedPaymentTokenResult
{
    public required bool Succeeded { get; init; }
    public required string PrimaryAccountNumber { get; init; } // DPAN / FPAN
    public required int ExpirationMonth { get; init; }
    public required int ExpirationYear { get; init; }
    public string? CardholderName { get; init; }
    public string? PaymentDataType { get; init; } // 3DSecure, EMV
    public string? ErrorMessage { get; init; }
}
