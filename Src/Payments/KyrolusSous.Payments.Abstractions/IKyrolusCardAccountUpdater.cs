namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusCardAccountUpdater
{
    Task<KyrolusAccountUpdateResult> CheckForUpdatesAsync(
        KyrolusAccountUpdateRequest request,
        CancellationToken cancellationToken = default);
}
