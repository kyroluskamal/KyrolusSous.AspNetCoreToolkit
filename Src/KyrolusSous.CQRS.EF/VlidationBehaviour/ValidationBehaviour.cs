using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.VlidationBehaviour;

public class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IKyrolusPipelineBehavior<TRequest, TResponse>
    where TRequest : IKyrolusCommand<TResponse>
    where TResponse : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();
        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
