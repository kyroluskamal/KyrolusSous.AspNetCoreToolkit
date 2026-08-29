namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusTokenizePanRequest
{
    public required string PrimaryAccountNumber { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required string CardholderName { get; init; }
}

public sealed record KyrolusNetworkTokenResult
{
    public required string NetworkTokenNumber { get; init; } // DPAN
    public required string TokenReferenceId { get; init; }
    public required string Cryptogram { get; init; } // TAVV / CAVV
    public required string EciFlag { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required bool IsActive { get; init; }
}
