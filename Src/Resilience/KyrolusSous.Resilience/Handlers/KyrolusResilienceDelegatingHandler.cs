using System.Net.Http;

namespace KyrolusSous.Resilience;

/// <summary>
/// DelegatingHandler that executes outgoing HTTP requests through a named Kyrolus resilience pipeline.
/// </summary>
public class KyrolusResilienceDelegatingHandler(
    IKyrolusResiliencePipelineProvider pipelineProvider,
    string pipelineName = "default") : DelegatingHandler
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(pipelineName);
        return pipeline.Execute(() => base.Send(request, cancellationToken));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(pipelineName);
        return await pipeline.ExecuteAsync(async ct => await base.SendAsync(request, ct), cancellationToken);
    }
}
