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

        return Task.FromResult(new KyrolusMerchantKycResult
        {
            MerchantId = submission.MerchantId,
            Status = KyrolusKycStatus.Approved,
            ApprovedTier = KyrolusKycTier.Tier3_Enterprise,
            MonthlyProcessingLimit = 1_000_000m
        });
    }
}
