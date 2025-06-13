using KyrolusSous.SourceMediator.Attributes;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.SourceMediator.Implementations
{
    /// <summary>
    /// Concrete implementation of <see cref="ISourceSender"/>.
    /// Uses DI to get an instance of the generated dispatcher logic (via IGeneratedDispatcher)
    /// and orchestrates the execution of registered pipeline behaviors.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceSender"/> class.
    /// </remarks>
    /// <param name="serviceProvider">The service provider instance.</param>
    /// <param name="generatedDispatcher">The dispatcher implementation (provided by DI via Source Generator registration).</param>
    /// <exception cref="ArgumentNullException">Thrown if serviceProvider or generatedDispatcher is null.</exception>
    public sealed class SourceSender(IServiceProvider serviceProvider, IGeneratedDispatcher generatedDispatcher) : ISourceSender // Not partial anymore
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly IGeneratedDispatcher _generatedDispatcher = generatedDispatcher ?? throw new ArgumentNullException(nameof(generatedDispatcher)); // Inject the INTERNAL interface

        // --- ISourceSender Implementation ---

        /// <inheritdoc />
        public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            // Execute pipeline, which will eventually call the dispatcher interface
            return BuildPipelineAndExecuteAsync<IQuery<TResponse>, TResponse>(query, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            // Execute pipeline, which will eventually call the dispatcher interface (expecting Unit)
            await BuildPipelineAndExecuteAsync<ICommand, Unit>(command, cancellationToken);
        }

        /// <inheritdoc />
        public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            // Execute pipeline, which will eventually call the dispatcher interface
            return BuildPipelineAndExecuteAsync<ICommand<TResponse>, TResponse>(command, cancellationToken);
        }

        // --- Pipeline Execution Logic ---

        /// <summary>
        /// Builds and executes the request pipeline, including behaviors.
        /// </summary>
        private Task<TResponse> BuildPipelineAndExecuteAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        {
            // 1. Resolve Behaviors
            var behaviorInterfaceType = typeof(IPipelineBehavior<,>).MakeGenericType(typeof(TRequest), typeof(TResponse));
            var behaviors = _serviceProvider.GetServices(behaviorInterfaceType)
                                          .Cast<object>()
                                          .ToList();

            // 2. Sort Behaviors
            behaviors.Sort((a, b) =>
            {
                var orderA = a.GetType().GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0;
                var orderB = b.GetType().GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0;
                return orderA.CompareTo(orderB);
            });

            // 3. Define the final action: calling the injected dispatcher interface implementation
            Task<TResponse?> handlerDelegate()
            {
                bool isCommandWithoutResponse = typeof(TResponse) == typeof(Unit) && request is ICommand;
                object requestAsObject = request!;

                if (isCommandWithoutResponse)
                {
                    // Call the command dispatcher via the interface and wrap Task in Task<Unit>
                    return Task.Run(async () =>
                    { // Using Task.Run just for consistency, can be direct call
                        await _generatedDispatcher.DispatchCommandAsync(requestAsObject, _serviceProvider, cancellationToken);
                        return default(TResponse); // Return Unit.Value cast to TResponse(Unit)
                    }, cancellationToken);
                }
                else
                {
                    // Call the request dispatcher via the interface
                    return _generatedDispatcher.DispatchRequestAsync<TResponse>(requestAsObject, _serviceProvider, cancellationToken)!;
                }
            }

            // 4. Build the pipeline chain (Aggregate)
            RequestHandlerDelegate<TResponse> pipeline = behaviors
                .Cast<IPipelineBehavior<TRequest, TResponse>>()
                .Reverse()
                .Aggregate((RequestHandlerDelegate<TResponse>)handlerDelegate!, (next, behavior) => () => behavior.Handle(request, next, cancellationToken));

            // 5. Execute the pipeline
            return pipeline();
        }
    }
}