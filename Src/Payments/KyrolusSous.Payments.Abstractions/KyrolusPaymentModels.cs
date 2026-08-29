namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusPaymentCustomer
{
    public string? CustomerId { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryCode { get; init; }
}

public sealed record KyrolusPaymentItem
{
    public required string Name { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; } = 1;
    public string? Sku { get; init; }
    public string? Description { get; init; }
}

public sealed record KyrolusPaymentRequest
{
    public required string OrderId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public string? Description { get; init; }
    public KyrolusPaymentCustomer? Customer { get; init; }
    public IReadOnlyList<KyrolusPaymentItem>? Items { get; init; }
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
    public string? WebhookUrl { get; init; }
    public KyrolusPaymentMethodType PreferredMethod { get; init; } = KyrolusPaymentMethodType.CreditCard;
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusPaymentResult
{
    public required string TransactionId { get; init; }
    public string? ProviderTransactionId { get; init; }
    public required KyrolusPaymentStatus Status { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public string? RedirectUrl { get; init; }
    public string? ReferenceCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, string> RawDetails { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool IsSuccess => Status is KyrolusPaymentStatus.Succeeded or KyrolusPaymentStatus.Processing;
    public bool RequiresRedirect => !string.IsNullOrEmpty(RedirectUrl);
}

public sealed record KyrolusRefundRequest
{
    public required string TransactionId { get; init; }
    public string? ProviderTransactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? Reason { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusRefundResult
{
    public required string RefundId { get; init; }
    public string? TransactionId { get; init; }
    public bool Succeeded { get; init; }
    public decimal? RefundedAmount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset RefundedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public record KyrolusWebhookEvent
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public required string ProviderName { get; init; }
    public string? TransactionId { get; init; }
    public KyrolusPaymentStatus? PaymentStatus { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? RawPayload { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
