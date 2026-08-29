namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusRiskLevel
{
    Normal,
    Elevated,
    High,
    Blocked
}

public enum KyrolusRiskAction
{
    Allow,
    Review,
    Require3DSecure,
    Block
}

public sealed record KyrolusRiskEvaluationRequest
{
    public required string OrderId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerIpAddress { get; init; }
    public string? CardBin { get; init; }
    public string? CardCountry { get; init; }
    public string? BillingCountry { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusRiskEvaluationResult
{
    public required int RiskScore { get; init; } // 0 to 100
    public required KyrolusRiskLevel RiskLevel { get; init; }
    public required KyrolusRiskAction RecommendedAction { get; init; }
    public IReadOnlyList<string> RiskReasons { get; init; } = [];
    public bool IsBlocked => RecommendedAction == KyrolusRiskAction.Block;
}
