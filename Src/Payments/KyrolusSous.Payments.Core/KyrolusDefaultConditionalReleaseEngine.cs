using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultConditionalReleaseEngine : IKyrolusConditionalReleaseEngine
{
    private readonly ConcurrentDictionary<string, KyrolusConditionalEscrowAgreement> _agreements = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterAgreement(KyrolusConditionalEscrowAgreement agreement)
    {
        _agreements[agreement.AgreementId] = agreement;
    }

    public KyrolusMilestoneReleaseResult TriggerMilestoneRelease(string agreementId, string milestoneId)
    {
        if (!_agreements.TryGetValue(agreementId, out var agreement))
        {
            throw new KeyNotFoundException($"Escrow agreement '{agreementId}' not found.");
        }

        var updatedMilestones = new List<KyrolusEscrowMilestone>();
        decimal newlyReleased = 0m;

        foreach (var m in agreement.Milestones)
        {
            if (m.MilestoneId.Equals(milestoneId, StringComparison.OrdinalIgnoreCase))
            {
                if (m.Status != KyrolusMilestoneStatus.Released)
                {
                    newlyReleased = m.AmountToRelease;
                    updatedMilestones.Add(m with { Status = KyrolusMilestoneStatus.Released, SatisfiedAtUtc = DateTimeOffset.UtcNow });
                }
                else
                {
                    updatedMilestones.Add(m);
                }
            }
            else
            {
                updatedMilestones.Add(m);
            }
        }

        var updatedAgreement = agreement with { Milestones = updatedMilestones.AsReadOnly() };
        _agreements[agreementId] = updatedAgreement;

        var totalReleased = updatedAgreement.Milestones.Where(m => m.Status == KyrolusMilestoneStatus.Released).Sum(m => m.AmountToRelease);
        var remaining = Math.Max(0m, updatedAgreement.TotalEscrowAmount - totalReleased);

        return new KyrolusMilestoneReleaseResult
        {
            AgreementId = agreementId,
            MilestoneId = milestoneId,
            ReleasedAmount = newlyReleased,
            RemainingLockedAmount = remaining,
            IsAgreementFullySettled = remaining == 0m
        };
    }
}
