namespace KyrolusSous.Mediator.Runtime.Internal;

/// <summary>
/// Caches the <see cref="PipelineOrderAttribute"/> lookup per behavior type. Behaviors are
/// transient, so the instances change every request, but their order never does.
/// </summary>
internal static class PipelineOrder
{
    private static readonly ConcurrentDictionary<Type, int> s_cache = new();

    public static int Of(object behavior) =>
        s_cache.GetOrAdd(behavior.GetType(), static type =>
            type.GetCustomAttribute<PipelineOrderAttribute>()?.Order ?? 0);
}
/// <summary>
/// Non-generic base so wrappers of any request type can share one cache dictionary.
/// </summary>
internal abstract class RequestPipelineWrapper
{
    public abstract Task<object?> HandleUntyped(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        CancellationToken cancellationToken);
}
/// <summary>
/// Typed by response only, so <see cref="KyrolusMediatorSender"/> can cast a cached wrapper
/// without knowing the request type.
/// </summary>
internal abstract class RequestPipelineWrapper<TResponse> : RequestPipelineWrapper
{
    public abstract Task<TResponse> Handle(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        CancellationToken cancellationToken);

    public override async Task<object?> HandleUntyped(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        CancellationToken cancellationToken)
        => await Handle(request, serviceProvider, dispatcher, cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Closed over both request and response, so resolving behaviors and chaining them needs no
/// reflection at all. One instance is built per (request, response) pair and reused forever.
/// </summary>
internal sealed class RequestPipelineWrapperImpl<TRequest, TResponse> : RequestPipelineWrapper<TResponse>
{
    public override Task<TResponse> Handle(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        Task<TResponse> Terminal(CancellationToken ct)
        {
            var effectiveCancelationToken = ct == default ? cancellationToken : ct;

            // A command with no response is dispatched through the command path, then adapted
            // back to Task<Unit> so the pipeline stays uniformly Task<TResponse>.
            if (typeof(TResponse) == typeof(Unit) && request is IKyrolusCommand)
            {
                return DispatchCommandAsUnitAsync(dispatcher, request, serviceProvider, effectiveCancelationToken);
            }

            return dispatcher.DispatchRequestAsync<TResponse>(request, serviceProvider, effectiveCancelationToken);
        }

        // OrderBy is a stable sort, so behaviors sharing an order keep their DI registration
        // order. List<T>.Sort is introsort and would scramble them unpredictably.
        var behaviors = serviceProvider
            .GetServices<IKyrolusPipelineBehavior<TRequest, TResponse>>()
            .OrderBy(PipelineOrder.Of)
            .ToArray();

        RequestHandlerDelegate<TResponse> next = Terminal;

        // Walk backwards so the lowest order ends up outermost.
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            // A behavior calling next() with no argument passes default; fall back to the token
            // the pipeline was started with rather than silently dropping cancellation.
            next = ct => behavior.Handle(typedRequest, inner, ct == default ? cancellationToken : ct);
        }

        return next(cancellationToken);
    }

    private static async Task<TResponse> DispatchCommandAsUnitAsync(
        IMediatorDispatcher dispatcher,
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        await dispatcher.DispatchCommandAsync(request, serviceProvider, cancellationToken).ConfigureAwait(false);
        return (TResponse)(object)Unit.Value;
    }
}

/// <summary>
/// Stream counterpart of <see cref="RequestPipelineWrapper{TResponse}"/>.
/// </summary>
internal abstract class StreamPipelineWrapper
{
    public abstract IAsyncEnumerable<object?> HandleUntyped(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        CancellationToken cancellationToken);
}

internal abstract class StreamPipelineWrapper<TResponse> : StreamPipelineWrapper
{
    public abstract IAsyncEnumerable<TResponse> Handle(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        CancellationToken cancellationToken);

    public override async IAsyncEnumerable<object?> HandleUntyped(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in Handle(request, serviceProvider, dispatcher, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return item;
        }
    }
}

internal sealed class StreamPipelineWrapperImpl<TRequest, TResponse> : StreamPipelineWrapper<TResponse>
{
    public override IAsyncEnumerable<TResponse> Handle(
        object request,
        IServiceProvider serviceProvider,
        IMediatorDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        var behaviors = serviceProvider
            .GetServices<IKyrolusStreamPipelineBehavior<TRequest, TResponse>>()
            .OrderBy(PipelineOrder.Of)
            .ToArray();

        StreamHandlerDelegate<TResponse> next = ct =>
            dispatcher.DispatchStreamAsync<TResponse>(request, serviceProvider, ct == default ? cancellationToken : ct);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = ct => behavior.Handle(typedRequest, inner, ct == default ? cancellationToken : ct);
        }

        return next(cancellationToken);
    }
}
