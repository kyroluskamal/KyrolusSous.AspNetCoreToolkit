namespace KyrolusSous.CQRS.ExceptionHandling;

public sealed class KyrolusExceptionMappingBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusExceptionMapper<TResponse>> mappers)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            foreach (var mapper in mappers)
            {
                if (mapper.TryMap(ex, out var mapped))
                {
                    return mapped;
                }
            }

            throw;
        }
    }
}
