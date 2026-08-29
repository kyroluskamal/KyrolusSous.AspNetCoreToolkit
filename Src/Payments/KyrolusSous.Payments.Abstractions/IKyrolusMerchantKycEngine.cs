namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusMerchantKycEngine
{
    Task<KyrolusMerchantKycResult> EvaluateKycSubmissionAsync(
        KyrolusMerchantKycSubmission submission,
        CancellationToken cancellationToken = default);
}
