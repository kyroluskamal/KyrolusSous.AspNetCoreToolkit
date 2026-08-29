namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusPaymentLinkRequest
{
    public required string Title { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public string? Description { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }
    public TimeSpan? ExpiresIn { get; init; }
    public bool AllowCustomAmount { get; init; } = false;
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusPaymentLinkResult
{
    public required string LinkId { get; init; }
    public required string Url { get; init; }
    public string? QrCodePayload { get; init; }
    public string? ReferenceCode { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public bool IsActive { get; init; } = true;
}
