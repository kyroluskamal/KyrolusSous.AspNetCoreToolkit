namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusAccountUpdateAction
{
    UpdatedExpiry,
    ReplacedCardNumber,
    ClosedAccount,
    NoChange
}

public sealed record KyrolusAccountUpdateRequest
{
    public required string CustomerId { get; init; }
    public required string PaymentMethodId { get; init; }
    public required string CurrentLast4 { get; init; }
    public required int CurrentExpiryMonth { get; init; }
    public required int CurrentExpiryYear { get; init; }
}

public sealed record KyrolusAccountUpdateResult
{
    public required string PaymentMethodId { get; init; }
    public required KyrolusAccountUpdateAction Action { get; init; }
    public string? NewLast4 { get; init; }
    public int? NewExpiryMonth { get; init; }
    public int? NewExpiryYear { get; init; }
    public bool HasChanged => Action != KyrolusAccountUpdateAction.NoChange;
}
