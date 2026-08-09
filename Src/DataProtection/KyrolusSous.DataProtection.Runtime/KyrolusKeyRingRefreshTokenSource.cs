namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusKeyRingRefreshTokenSource
{
    private readonly object gate = new();
    private CancellationTokenSource externalSource = new();
    private CancellationTokenSource? linkedSource;

    public CancellationToken GetToken(CancellationToken innerToken)
    {
        lock (gate)
        {
            if (linkedSource is null || linkedSource.IsCancellationRequested)
            {
                linkedSource?.Dispose();
                linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    innerToken,
                    externalSource.Token);
            }

            return linkedSource.Token;
        }
    }

    public void SignalExternal()
    {
        CancellationTokenSource? toCancel;

        lock (gate)
        {
            toCancel = externalSource;
            externalSource = new CancellationTokenSource();
        }

        try
        {
            toCancel.Cancel();
        }
        finally
        {
            toCancel.Dispose();
        }
    }
}
