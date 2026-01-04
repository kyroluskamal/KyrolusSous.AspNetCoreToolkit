namespace KyrolusSous.DataProtection.Abstractions;

public interface IKyrolusKeyRingRefreshHook
{
    Task OnKeyRingRefreshedAsync(
        KyrolusKeyRingRefreshContext context,
        CancellationToken cancellationToken = default);
}
