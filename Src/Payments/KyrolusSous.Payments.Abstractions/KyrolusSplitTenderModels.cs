namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusTenderLeg
{
    public required string ProviderName { get; init; } // e.g. "Wallet", "Stripe"
    public required decimal Amount { get; init; }
    public string? PaymentMethodId { get; init; }
}

public sealed record KyrolusSplitTenderRequest
{
    public required string OrderId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<KyrolusTenderLeg> Legs { get; init; }
    public string? Description { get; init; }
}

public sealed record KyrolusTenderLegResult
{
    public required string ProviderName { get; init; }
    public required decimal Amount { get; init; }
    public required bool Succeeded { get; init; }
    public string? TransactionId { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record KyrolusSplitTenderResult
{
    public required string OrderId { get; init; }
    public required bool Succeeded { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public IReadOnlyList<KyrolusTenderLegResult> LegResults { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
