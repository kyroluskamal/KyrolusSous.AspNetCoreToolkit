namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusConditionalReleaseEngine
{
    void RegisterAgreement(KyrolusConditionalEscrowAgreement agreement);
    KyrolusMilestoneReleaseResult TriggerMilestoneRelease(string agreementId, string milestoneId);
}
