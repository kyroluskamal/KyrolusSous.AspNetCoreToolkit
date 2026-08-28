using Polly;

namespace KyrolusSous.Resilience;

/// <summary>
/// Default implementation of <see cref="IKyrolusResiliencePipelineComposer"/> chaining multiple pipelines sequentially.
/// </summary>
public class KyrolusResiliencePipelineComposer(IKyrolusResiliencePipelineProvider pipelineProvider) : IKyrolusResiliencePipelineComposer
{
    public ResiliencePipeline Compose(params string[] pipelineNames)
    {
        ArgumentNullException.ThrowIfNull(pipelineNames);
        if (pipelineNames.Length == 0)
        {
            return pipelineProvider.GetPipeline("default");
        }

        var builder = new ResiliencePipelineBuilder();
        foreach (var name in pipelineNames)
        {
            var pipeline = pipelineProvider.GetPipeline(name);
            builder.AddPipeline(pipeline);
        }

        return builder.Build();
    }

    public ResiliencePipeline<TResult> Compose<TResult>(params string[] pipelineNames)
    {
        ArgumentNullException.ThrowIfNull(pipelineNames);
        if (pipelineNames.Length == 0)
        {
            return pipelineProvider.GetPipeline<TResult>("default");
        }

        var builder = new ResiliencePipelineBuilder<TResult>();
        foreach (var name in pipelineNames)
        {
            var pipeline = pipelineProvider.GetPipeline<TResult>(name);
            builder.AddPipeline(pipeline);
        }

        return builder.Build();
    }
}
