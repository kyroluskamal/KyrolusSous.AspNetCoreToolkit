using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultFraudDetectionEngine : IKyrolusFraudDetectionEngine
{
    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "tempmail.com", "guerrillamail.com", "10minutemail.com", "trashmail.com", "throwawaymail.com"
    };

    public Task<KyrolusRiskEvaluationResult> EvaluateRiskAsync(
        KyrolusRiskEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var score = 10; // baseline
        var reasons = new List<string>();

        // 1. Check disposable email
        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            var parts = request.CustomerEmail.Split('@');
            if (parts.Length == 2 && DisposableDomains.Contains(parts[1]))
            {
                score += 50;
                reasons.Add($"Disposable email domain detected: {parts[1]}");
            }
        }

        // 2. Country mismatch
        if (!string.IsNullOrWhiteSpace(request.CardCountry) &&
            !string.IsNullOrWhiteSpace(request.BillingCountry) &&
            !string.Equals(request.CardCountry, request.BillingCountry, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
            reasons.Add($"Country mismatch: Card issued in {request.CardCountry} but billing in {request.BillingCountry}");
        }

        // 3. High transaction amount
        if (request.Amount > 5000m)
        {
            score += 20;
            reasons.Add($"High value transaction: {request.Amount} {request.Currency}");
        }

        // 4. Missing IP address
        if (string.IsNullOrWhiteSpace(request.CustomerIpAddress))
        {
            score += 10;
            reasons.Add("Missing customer IP address");
        }

        score = Math.Clamp(score, 0, 100);

        var (level, action) = score switch
        {
            >= 80 => (KyrolusRiskLevel.Blocked, KyrolusRiskAction.Block),
            >= 50 => (KyrolusRiskLevel.High, KyrolusRiskAction.Require3DSecure),
            >= 30 => (KyrolusRiskLevel.Elevated, KyrolusRiskAction.Review),
            _ => (KyrolusRiskLevel.Normal, KyrolusRiskAction.Allow)
        };

        return Task.FromResult(new KyrolusRiskEvaluationResult
        {
            RiskScore = score,
            RiskLevel = level,
            RecommendedAction = action,
            RiskReasons = reasons.AsReadOnly()
        });
    }
}
