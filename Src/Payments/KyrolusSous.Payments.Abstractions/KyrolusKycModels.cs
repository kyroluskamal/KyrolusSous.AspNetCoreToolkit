namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusKycTier
{
    Tier1_Starter, // Up to $5,000 / month
    Tier2_Verified, // Up to $50,000 / month
    Tier3_Enterprise // Unlimited
}

public enum KyrolusKycStatus
{
    UnderReview,
    Approved,
    Rejected,
    ActionRequired
}

public sealed record KyrolusMerchantKycSubmission
{
    public required string MerchantId { get; init; }
    public required string LegalBusinessName { get; init; }
    public required string TaxRegistrationNumber { get; init; }
    public required string CommercialRegisterNumber { get; init; }
    public required string BeneficialOwnerName { get; init; }
    public required string BeneficialOwnerNationalIdOrPassport { get; init; }
    public required string CountryCode { get; init; }
    public IDictionary<string, string> DocumentHashes { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusMerchantKycResult
{
    public required string MerchantId { get; init; }
    public required KyrolusKycStatus Status { get; init; }
    public required KyrolusKycTier ApprovedTier { get; init; }
    public decimal MonthlyProcessingLimit { get; init; }
    public IReadOnlyList<string> RequiredAdditionalDocuments { get; init; } = [];
    public string? RejectionReason { get; init; }
}
