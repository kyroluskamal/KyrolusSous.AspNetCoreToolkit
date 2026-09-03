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

        // Check if request or response implements IKyrolusProjectableCommand
        if (request is not null)
            await TryProjectAsync(request, cancellationToken).ConfigureAwait(false);

        if (response is not null && !ReferenceEquals(response, request))
            await TryProjectAsync(response, cancellationToken).ConfigureAwait(false);

        return response;
    }

    private async Task TryProjectAsync(object request, CancellationToken cancellationToken)
    {
        foreach (var projectableInterface in GetProjectableInterfaces(request))
        {
            var readModel = GetReadModel(request, projectableInterface);
            if (readModel is null) continue;

            await ProjectReadModelAsync(readModel, projectableInterface, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<Type> GetProjectableInterfaces(object request)
    {
        return request.GetType().GetInterfaces().Where(IsProjectableCommandInterface);

        static bool IsProjectableCommandInterface(Type iface) =>
            iface.IsGenericType &&
            iface.GetGenericTypeDefinition() == typeof(IKyrolusProjectableCommand<>);
    }

    private static object? GetReadModel(object request, Type projectableInterface)
    {
        var toReadModelMethod = projectableInterface.GetMethod(nameof(IKyrolusProjectableCommand<object>.ToReadModel));
        if (toReadModelMethod is null)
            return null;

        return toReadModelMethod.Invoke(request, null);
    }

    private async Task ProjectReadModelAsync(object readModel, Type projectableInterface, CancellationToken cancellationToken)
    {
        var readModelType = projectableInterface.GetGenericArguments()[0];
        var projectorType = typeof(IReadModelProjector<>).MakeGenericType(readModelType);
        var projectors = _serviceProvider.GetServices(projectorType);

        foreach (var projector in projectors)
        {
            if (projector is null)
                continue;

            await TryProjectWithProjectorAsync(projector, readModel, projectorType, readModelType, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task TryProjectWithProjectorAsync(
        object projector,
        object readModel,
        Type projectorType,
        Type readModelType,
        CancellationToken cancellationToken)
    {
        try
        {
            var projectMethod = projectorType.GetMethod(nameof(IReadModelProjector<object>.ProjectAsync));
            if (projectMethod is null)
                return;

            var task = (Task?)projectMethod.Invoke(projector, [readModel, cancellationToken]);
            if (task is null)
                return;

            await task.ConfigureAwait(false);
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
