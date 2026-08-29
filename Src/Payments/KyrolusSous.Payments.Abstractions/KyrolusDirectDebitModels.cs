namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusDirectDebitScheme
{
    SepaDirectDebit,
    UsAchDebit,
    UkBacs,
    EgyptIpnPull
}

public enum KyrolusMandateStatus
{
    PendingSignature,
    Active,
    Revoked,
    Expired
}

public sealed record KyrolusCreateMandateRequest
{
    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required string CustomerIbanOrAccountNumber { get; init; }
    public string? BankBicOrRoutingNumber { get; init; }
    public required KyrolusDirectDebitScheme Scheme { get; init; }
    public required string Currency { get; init; }
    public decimal? MaximumDebitAmountPerTransaction { get; init; }
}

public sealed record KyrolusDirectDebitMandate
{
    public required string MandateId { get; init; }
    public required string CustomerId { get; init; }
    public required string MandateReference { get; init; }
    public required KyrolusDirectDebitScheme Scheme { get; init; }
    public required KyrolusMandateStatus Status { get; init; }
    public required string Currency { get; init; }
    public DateTimeOffset SignedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record KyrolusExecuteDebitResult
{
    public required string TransactionId { get; init; }
    public required string MandateId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset EstimatedSettlementDateUtc { get; init; }
}
