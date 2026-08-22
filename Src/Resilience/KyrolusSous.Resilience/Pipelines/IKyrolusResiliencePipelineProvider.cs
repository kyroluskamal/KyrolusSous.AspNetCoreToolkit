namespace KyrolusSous.Resilience;

public interface IKyrolusResiliencePipelineProvider
{
    ResiliencePipeline GetPipeline(string name = "default");

    ResiliencePipeline<TResult> GetPipeline<TResult>(string name = "default");
}
