using System.Text;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultChargebackDefenseEngine : IKyrolusChargebackDefenseEngine
{
    public KyrolusChargebackSubmissionResult ValidateAndCompileEvidence(KyrolusChargebackEvidenceBundle bundle)
    {
        var missing = new List<string>();
        int score = 0;

        if (!string.IsNullOrWhiteSpace(bundle.CustomerEmail)) score += 20;
        else missing.Add("CustomerEmail");

        if (!string.IsNullOrWhiteSpace(bundle.CustomerIpAddress)) score += 20;
        else missing.Add("CustomerIpAddress");

        if (!string.IsNullOrWhiteSpace(bundle.ShippingTrackingNumber) || !string.IsNullOrWhiteSpace(bundle.ProofOfServiceOrDownloadUrl))
        {
            score += 30;
        }
        else
        {
            missing.Add("ProofOfFulfillmentOrDelivery");
        }

        if (!string.IsNullOrWhiteSpace(bundle.TermsOfServiceAcceptanceTimestamp)) score += 15;
        if (!string.IsNullOrWhiteSpace(bundle.PriorUndisputedTransactionId)) score += 15;

        var sb = new StringBuilder();
        sb.AppendLine($"Chargeback Defense Brief for Dispute [{bundle.DisputeId}]");
        sb.AppendLine($"Order ID: {bundle.OrderId}");
        sb.AppendLine($"Customer: {bundle.CustomerEmail} (IP: {bundle.CustomerIpAddress})");
        if (!string.IsNullOrWhiteSpace(bundle.ShippingTrackingNumber))
            sb.AppendLine($"Fulfillment: Carrier {bundle.CarrierName} Tracking #{bundle.ShippingTrackingNumber}");
        if (!string.IsNullOrWhiteSpace(bundle.ProofOfServiceOrDownloadUrl))
            sb.AppendLine($"Digital Download / Access Proof: {bundle.ProofOfServiceOrDownloadUrl}");
        if (!string.IsNullOrWhiteSpace(bundle.TermsOfServiceAcceptanceTimestamp))
            sb.AppendLine($"Terms Accepted At: {bundle.TermsOfServiceAcceptanceTimestamp}");

        return new KyrolusChargebackSubmissionResult
        {
            DisputeId = bundle.DisputeId,
            IsReadyForSubmission = score >= 50,
            EvidenceCompletenessScorePercent = Math.Min(100, score),
            MissingCriticalItems = missing.AsReadOnly(),
            CompiledDefenseSummaryText = sb.ToString()
        };
    }
}
