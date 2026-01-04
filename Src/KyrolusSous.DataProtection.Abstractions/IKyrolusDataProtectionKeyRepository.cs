namespace KyrolusSous.DataProtection.Abstractions;

public interface IKyrolusDataProtectionKeyRepository
{
    Task<IReadOnlyList<KyrolusDataProtectionKeyDocument>> ExportAsync(CancellationToken cancellationToken = default);
    Task ImportAsync(IEnumerable<KyrolusDataProtectionKeyDocument> documents, CancellationToken cancellationToken = default);
}
