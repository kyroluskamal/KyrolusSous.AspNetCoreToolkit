using Polly;

namespace KyrolusSous.Resilience;

/// <summary>
/// Provider contract for resolving named and typed Polly resilience pipelines.
/// </summary>
public interface IKyrolusResiliencePipelineProvider
{
    /// <summary>
    /// Gets or creates a resilience pipeline by name.
    /// </summary>
    ResiliencePipeline GetPipeline(string name = "default");

    /// <summary>
    /// Gets or creates a generic resilience pipeline with typed result handling by name.
    /// </summary>
    ResiliencePipeline<TResult> GetPipeline<TResult>(string name = "default");
}
