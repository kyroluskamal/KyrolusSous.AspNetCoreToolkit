namespace KyrolusSous.Mediator.Runtime.Implementations
{
    /// <summary>
    /// Concrete implementation of <see cref="IKyrolusMediatorSender"/>.
    /// Uses DI to get an instance of dispatcher logic (generated or reflection-based)
    /// and orchestrates the execution of registered pipeline behaviors.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="KyrolusMediatorSender"/> class.
    /// </remarks>
    /// <param name="serviceProvider">The service provider instance.</param>
    /// <param name="generatedDispatcher">The dispatcher implementation (generated or reflection-based).</param>
    /// <exception cref="ArgumentNullException">Thrown if serviceProvider or generatedDispatcher is null.</exception>
    public sealed class KyrolusMediatorSender(IServiceProvider serviceProvider, IGeneratedDispatcher generatedDispatcher) : IKyrolusMediatorSender // Not partial anymore
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly IGeneratedDispatcher _generatedDispatcher = generatedDispatcher ?? throw new ArgumentNullException(nameof(generatedDispatcher)); // Inject the INTERNAL interface

        // --- IKyrolusMediatorSender Implementation ---

        /// <inheritdoc />
        public Task<TResponse> SendAsync<TResponse>(IKyrolusQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            // Execute pipeline, which will eventually call the dispatcher interface
            return BuildPipelineAndExecuteAsync<IKyrolusQuery<TResponse>, TResponse>(query, cancellationToken);
        }

        /// <inheritdoc />
        public Task<TResponse> SendAsync<TResponse>(IKyrolusRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request is IKyrolusQuery<TResponse> query)
            {
                return SendAsync(query, cancellationToken);
            }

            if (request is IKyrolusCommand<TResponse> commandWithResponse)
            {
                return SendAsync(commandWithResponse, cancellationToken);
            }

            if (request is IKyrolusCommand command && typeof(TResponse) == typeof(Unit))
            {
                return SendCommandAsUnitAsync<TResponse>(command, cancellationToken);
            }

            return BuildPipelineAndExecuteAsync<IKyrolusRequest<TResponse>, TResponse>(request, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SendAsync(IKyrolusCommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            // Execute pipeline, which will eventually call the dispatcher interface (expecting Unit)
            await BuildPipelineAndExecuteAsync<IKyrolusCommand, Unit>(command, cancellationToken);
        }

        /// <inheritdoc />
        public Task<TResponse> SendAsync<TResponse>(IKyrolusCommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            // Execute pipeline, which will eventually call the dispatcher interface
            return BuildPipelineAndExecuteAsync<IKyrolusCommand<TResponse>, TResponse>(command, cancellationToken);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IKyrolusStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return BuildStreamPipelineAndExecute<IKyrolusStreamRequest<TResponse>, TResponse>(request, cancellationToken);
        }

        private async Task<TResponse> SendCommandAsUnitAsync<TResponse>(IKyrolusCommand command, CancellationToken cancellationToken)
        {
            await SendAsync(command, cancellationToken).ConfigureAwait(false);
            return (TResponse)(object)Unit.Value;
        }

        // --- Pipeline Execution Logic ---

        /// <summary>
        /// Builds and executes the request pipeline, including behaviors.
        /// </summary>
        private Task<TResponse> BuildPipelineAndExecuteAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        {
            // 1. Resolve Behaviors
            var behaviorInterfaceType = typeof(IKyrolusPipelineBehavior<,>).MakeGenericType(typeof(TRequest), typeof(TResponse));
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
                bool isCommandWithoutResponse = typeof(TResponse) == typeof(Unit) && request is IKyrolusCommand;
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
                .Cast<IKyrolusPipelineBehavior<TRequest, TResponse>>()
                .Reverse()
                .Aggregate((RequestHandlerDelegate<TResponse>)handlerDelegate!, (next, behavior) => () => behavior.Handle(request, next, cancellationToken));

            // 5. Execute the pipeline
            return pipeline();
        }

        private IAsyncEnumerable<TResponse> BuildStreamPipelineAndExecute<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        {
            var behaviorInterfaceType = typeof(IKyrolusStreamPipelineBehavior<,>).MakeGenericType(typeof(TRequest), typeof(TResponse));
            var behaviors = _serviceProvider.GetServices(behaviorInterfaceType)
                                          .Cast<object>()
                                          .ToList();

            behaviors.Sort((a, b) =>
            {
                var orderA = a.GetType().GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0;
                var orderB = b.GetType().GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0;
                return orderA.CompareTo(orderB);
            });

            StreamHandlerDelegate<TResponse> handlerDelegate = ct =>
                _generatedDispatcher.DispatchStreamAsync<TResponse>(request!, _serviceProvider, ct);

            var pipeline = behaviors
                .Cast<IKyrolusStreamPipelineBehavior<TRequest, TResponse>>()
                .Reverse()
                .Aggregate(handlerDelegate, (next, behavior) => ct => behavior.Handle(request, next, ct));

            return pipeline(cancellationToken);
        }
    }
}
