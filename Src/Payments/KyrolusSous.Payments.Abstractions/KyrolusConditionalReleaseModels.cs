namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusMilestoneStatus
{
    Pending,
    Satisfied,
    Released,
    Cancelled
}

public sealed record KyrolusEscrowMilestone
{
    public required string MilestoneId { get; init; }
    public required string Description { get; init; }
    public required decimal AmountToRelease { get; init; }
    public required KyrolusMilestoneStatus Status { get; init; } = KyrolusMilestoneStatus.Pending;
    public DateTimeOffset? SatisfiedAtUtc { get; init; }
}

public sealed record KyrolusConditionalEscrowAgreement
{
    public required string AgreementId { get; init; }
    public required string SellerId { get; init; }
    public required decimal TotalEscrowAmount { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<KyrolusEscrowMilestone> Milestones { get; init; }
}

public sealed record KyrolusMilestoneReleaseResult
{
    public required string AgreementId { get; init; }
    public required string MilestoneId { get; init; }
    public required decimal ReleasedAmount { get; init; }
    public required decimal RemainingLockedAmount { get; init; }
    public required bool IsAgreementFullySettled { get; init; }
}
