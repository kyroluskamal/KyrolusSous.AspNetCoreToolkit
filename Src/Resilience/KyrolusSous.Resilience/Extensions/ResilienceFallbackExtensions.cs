namespace KyrolusSous.Resilience;

public static class ResilienceFallbackExtensions
{
    public static async ValueTask<TResult> ExecuteWithFallbackAsync<TResult>(
        this IKyrolusResiliencePipelineProvider provider,
        Func<CancellationToken, ValueTask<TResult>> action,
        Func<Exception, CancellationToken, ValueTask<TResult>> fallback,
        string pipelineName = "default",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(fallback);

        var pipeline = provider.GetPipeline<TResult>(pipelineName);

        try
        {
            return await pipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return await fallback(ex, cancellationToken);
        }
    }

    public static async ValueTask ExecuteWithFallbackAsync(
        this IKyrolusResiliencePipelineProvider provider,
        Func<CancellationToken, ValueTask> action,
        Func<Exception, CancellationToken, ValueTask> fallback,
        string pipelineName = "default",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(fallback);

        var pipeline = provider.GetPipeline(pipelineName);

        try
        {
            await pipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await fallback(ex, cancellationToken);
        }
    }
}
