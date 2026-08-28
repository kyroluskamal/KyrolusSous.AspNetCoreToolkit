using Polly;

namespace KyrolusSous.Resilience;

/// <summary>
/// Composes multiple named resilience pipelines into a single unified hierarchical execution pipeline.
/// </summary>
public interface IKyrolusResiliencePipelineComposer
{
    /// <summary>
    /// Composes multiple non-generic pipelines in sequence.
    /// </summary>
    ResiliencePipeline Compose(params string[] pipelineNames);

    /// <summary>
    /// Composes multiple generic pipelines in sequence for a specified result type.
    /// </summary>
    ResiliencePipeline<TResult> Compose<TResult>(params string[] pipelineNames);
}
