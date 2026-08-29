namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusChargebackDefenseEngine
{
    KyrolusChargebackSubmissionResult ValidateAndCompileEvidence(KyrolusChargebackEvidenceBundle bundle);
}
