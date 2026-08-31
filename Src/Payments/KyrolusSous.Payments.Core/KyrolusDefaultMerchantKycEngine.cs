using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultMerchantKycEngine : IKyrolusMerchantKycEngine
{
    public Task<KyrolusMerchantKycResult> EvaluateKycSubmissionAsync(
        KyrolusMerchantKycSubmission submission,
        CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(submission.TaxRegistrationNumber))
            missing.Add("TaxRegistrationCertificate");

        if (string.IsNullOrWhiteSpace(submission.CommercialRegisterNumber))
            missing.Add("CommercialRegistrationExtract");

        if (string.IsNullOrWhiteSpace(submission.BeneficialOwnerNationalIdOrPassport))
            missing.Add("NationalIdOrPassportCopy");

        if (missing.Count > 0)
        {
            return Task.FromResult(new KyrolusMerchantKycResult
            {
                MerchantId = submission.MerchantId,
                Status = KyrolusKycStatus.ActionRequired,
                ApprovedTier = KyrolusKycTier.Tier1_Starter,
                MonthlyProcessingLimit = 5000m,
                RequiredAdditionalDocuments = missing.AsReadOnly()
            });
        }

        // Presence of these fields only proves the submission is well-formed, not that the merchant's
        // identity/documents are genuine. This default engine performs no real verification (no
        // sanctions/PEP screening, no document authenticity or registry check), so it must not
        // auto-approve a high processing tier - it can only queue the submission for real review.
        return Task.FromResult(new KyrolusMerchantKycResult
        {
            MerchantId = submission.MerchantId,
            Status = KyrolusKycStatus.UnderReview,
            ApprovedTier = KyrolusKycTier.Tier1_Starter,
            MonthlyProcessingLimit = 5000m
        });
    }
}
