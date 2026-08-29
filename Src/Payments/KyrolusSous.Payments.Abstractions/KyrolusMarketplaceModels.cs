namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusMerchantAccountRequest
{
    public required string Email { get; init; }
    public required string CountryCode { get; init; }
    public string? BusinessName { get; init; }
    public string? ReturnUrl { get; init; }
    public string? RefreshUrl { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusMerchantAccountResult
{
    public required string AccountId { get; init; }
    public string? OnboardingUrl { get; init; }
    public bool IsPayoutsEnabled { get; init; } = false;
    public bool IsChargesEnabled { get; init; } = false;
    public string? ErrorMessage { get; init; }
}

public sealed record KyrolusSplitTransferRequest
{
    public required string DestinationAccountId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public decimal? PlatformFeeAmount { get; init; }
    public string? SourceTransactionId { get; init; }
    public string? Description { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusSplitTransferResult
{
    public required string TransferId { get; init; }
    public required string DestinationAccountId { get; init; }
    public decimal Amount { get; init; }
    public decimal PlatformFeeAmount { get; init; }
    public string? Currency { get; init; }
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
