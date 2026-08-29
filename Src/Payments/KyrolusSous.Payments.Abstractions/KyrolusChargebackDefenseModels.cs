namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusChargebackEvidenceBundle
{
    public required string DisputeId { get; init; }
    public required string OrderId { get; init; }
    public required string CustomerEmail { get; init; }
    public required string CustomerIpAddress { get; init; }
    public string? ShippingTrackingNumber { get; init; }
    public string? CarrierName { get; init; }
    public DateTimeOffset? DeliveryDateUtc { get; init; }
    public string? ProofOfServiceOrDownloadUrl { get; init; }
    public string? TermsOfServiceAcceptanceTimestamp { get; init; }
    public string? PriorUndisputedTransactionId { get; init; }
    public IDictionary<string, string> AdditionalDocuments { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusChargebackSubmissionResult
{
    public required string DisputeId { get; init; }
    public required bool IsReadyForSubmission { get; init; }
    public int EvidenceCompletenessScorePercent { get; init; }
    public IReadOnlyList<string> MissingCriticalItems { get; init; } = [];
    public string? CompiledDefenseSummaryText { get; init; }
}
