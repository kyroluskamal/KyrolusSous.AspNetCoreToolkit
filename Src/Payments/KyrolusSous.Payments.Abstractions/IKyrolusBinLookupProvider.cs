namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusBinLookupProvider
{
    Task<KyrolusBinLookupResult> LookupBinAsync(string binOrCardNumber, CancellationToken cancellationToken = default);
}
