namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusDisputeStatus
{
    NeedsResponse,
    UnderReview,
    Won,
    Lost,
    Accepted
}

public sealed record KyrolusDispute
{
    public required string DisputeId { get; init; }
    public required string TransactionId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Reason { get; init; }
    public required KyrolusDisputeStatus Status { get; init; }
    public DateTimeOffset? DueByUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record KyrolusSubmitDisputeEvidenceRequest
{
    public required string DisputeId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? ExplanationText { get; init; }
    public string? ReceiptUrl { get; init; }
    public string? TrackingNumber { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusDisputeEvidenceResult
{
    public required string DisputeId { get; init; }
    public required KyrolusDisputeStatus Status { get; init; }
    public bool IsSubmitted { get; init; }
    public string? Message { get; init; }
}
