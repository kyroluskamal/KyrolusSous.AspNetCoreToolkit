using System.Reflection;
using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.Resilience;

public class ResiliencePipelineBehavior<TRequest, TResponse>(
    IKyrolusResiliencePipelineProvider pipelineProvider) : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attr = typeof(TRequest).GetCustomAttribute<ResilientAttribute>(true);
        var resilientReq = request as IResilientRequest;

        if (attr is null && resilientReq is null)
        {
            return await next(cancellationToken);
        }

        var pipelineName = attr?.PipelineName ?? resilientReq?.PipelineName ?? "default";
        var pipeline = pipelineProvider.GetPipeline<TResponse>(pipelineName);

        return await pipeline.ExecuteAsync(async ct => await next(ct), cancellationToken);
    }
}
