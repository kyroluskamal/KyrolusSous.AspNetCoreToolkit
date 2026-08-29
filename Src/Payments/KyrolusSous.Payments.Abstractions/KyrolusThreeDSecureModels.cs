namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusThreeDSecureFlow
{
    Frictionless,
    ChallengeRequired,
    ExemptedLowValue,
    ExemptedTrustedMerchant
}

public sealed record KyrolusThreeDSecureEvaluationRequest
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string CardholderIpAddress { get; init; }
    public required string BrowserUserAgent { get; init; }
    public bool IsRecurringTransaction { get; init; } = false;
    public bool IsTrustedBeneficiary { get; init; } = false;
}

public sealed record KyrolusThreeDSecureEvaluationResult
{
    public required KyrolusThreeDSecureFlow RecommendedFlow { get; init; }
    public required string EciFlag { get; init; } // Electronic Commerce Indicator (e.g. "05", "06", "07")
    public string? ChallengeUrl { get; init; }
    public string? ExemptionReason { get; init; }
    public bool RequiresOtpPrompt => RecommendedFlow == KyrolusThreeDSecureFlow.ChallengeRequired;
}
