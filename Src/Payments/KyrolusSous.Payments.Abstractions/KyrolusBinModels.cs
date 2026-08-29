namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusCardScheme
{
    Visa,
    Mastercard,
    Meeza,
    AmericanExpress,
    Discover,
    UnionPay,
    Unknown
}

public enum KyrolusCardType
{
    Credit,
    Debit,
    Prepaid,
    Unknown
}

public sealed record KyrolusBinLookupResult
{
    public required string Bin { get; init; }
    public required KyrolusCardScheme Scheme { get; init; }
    public required KyrolusCardType CardType { get; init; }
    public string? BankName { get; init; }
    public string? CountryCode { get; init; }
    public string? CountryName { get; init; }
    public bool IsCommercial { get; init; } = false;
}
