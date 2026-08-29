namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusVaultCustomer
{
    public required string CustomerId { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? DefaultPaymentMethodId { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusSavedPaymentMethod
{
    public required string PaymentMethodId { get; init; }
    public required string CustomerId { get; init; }
    public required KyrolusPaymentMethodType MethodType { get; init; }
    public string? LastFourDigits { get; init; }
    public string? CardBrand { get; init; }
    public int? ExpirationMonth { get; init; }
    public int? ExpirationYear { get; init; }
    public bool IsDefault { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record KyrolusSavePaymentMethodRequest
{
    public required string CustomerId { get; init; }
    public required string PaymentTokenOrNonce { get; init; }
    public bool SetAsDefault { get; init; } = true;
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusVaultResult
{
    public required bool Succeeded { get; init; }
    public string? CustomerId { get; init; }
    public string? PaymentMethodId { get; init; }
    public string? ClientSecretOrSetupUrl { get; init; }
    public string? ErrorMessage { get; init; }
}
