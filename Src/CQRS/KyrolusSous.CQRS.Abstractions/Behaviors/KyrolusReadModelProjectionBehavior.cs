using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Projections;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior synchronizing read-model projections upon successful command execution.
/// </summary>
[PipelineOrder(-600)]
public sealed class KyrolusReadModelProjectionBehavior<TRequest, TResponse>(
    IServiceProvider serviceProvider,
    ILogger<KyrolusReadModelProjectionBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        // Check if request or response implements IProjectableCommand
        if (request is not null)
        {
            await TryProjectAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (response is not null && !ReferenceEquals(response, request))
        {
            await TryProjectAsync(response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private async Task TryProjectAsync(object request, CancellationToken cancellationToken)
    {
        var interfaces = request.GetType().GetInterfaces();
        foreach (var iface in interfaces)
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IProjectableCommand<>))
            {
                var readModelType = iface.GetGenericArguments()[0];
                var toReadModelMethod = iface.GetMethod(nameof(IProjectableCommand<object>.ToReadModel));
                if (toReadModelMethod is null) continue;

                var readModel = toReadModelMethod.Invoke(request, null);
                if (readModel is null) continue;

                var projectorType = typeof(IReadModelProjector<>).MakeGenericType(readModelType);
                var projectors = _serviceProvider.GetServices(projectorType);

                foreach (var projector in projectors)
                {
                    if (projector is null) continue;
                    try
                    {
                        var projectMethod = projectorType.GetMethod(nameof(IReadModelProjector<object>.ProjectAsync));
                        if (projectMethod is not null)
                        {
                            var task = (Task?)projectMethod.Invoke(projector, [readModel, cancellationToken]);
                            if (task is not null)
                            {
                                await task.ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(
                            ex,
                            "[Kyrolus CQRS Projection] Failed to synchronize read model '{ReadModelType}' via '{ProjectorType}'",
                            readModelType.Name,
                            projector.GetType().Name);
                    }
                }
            }
        }
    }
}
