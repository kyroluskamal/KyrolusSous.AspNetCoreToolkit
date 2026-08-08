using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Mediator.Reflection;
using KyrolusSous.Mediator.Runtime.Config;
using KyrolusSous.Mediator.Runtime.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunMediatorRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = 0;

        ExpectThrows<ArgumentException>(
            () => new ServiceCollection().AddKyrolusMediatorFromAssemblies([]),
            "Mediator registration should require at least one assembly.",
            ref checks);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<MediatorRuntimeState>();
        services.AddKyrolusMediatorFromAssemblies(typeof(MediatorProbeQuery).Assembly);

        using var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        var sender = provider.GetRequiredService<IKyrolusMediatorSender>();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();
        var dispatcher = provider.GetRequiredService<IMediatorDispatcher>();
        var state = provider.GetRequiredService<MediatorRuntimeState>();

        Require(
            provider.GetRequiredService<IKyrolusNotificationPublishStrategy>() is KyrolusParallelNotificationPublishStrategy,
            "Parallel notification strategy should be the default.",
            ref checks);
        var notificationServices = new ServiceCollection();
        Require(
            ReferenceEquals(notificationServices, notificationServices.UseKyrolusMediatorSequentialNotifications()) &&
            ReferenceEquals(notificationServices, notificationServices.UseKyrolusMediatorParallelNotifications()),
            "Mediator notification strategy helpers should return the same service collection for chaining.",
            ref checks);

        var queryRequestTypeName = typeof(MediatorProbeQuery).Name;

        var queryResult = await mediator.SendAsync(new MediatorProbeQuery("alpha"), cancellationToken).ConfigureAwait(false);
        Require(queryResult == "query:alpha", "Query handler should return the expected result.", ref checks);
        Require(
            state.Events.Contains($"outer-before:{queryRequestTypeName}") &&
            state.Events.Contains("pre:alpha") &&
            state.Events.Contains($"inner-before:{queryRequestTypeName}") &&
            state.Events.Contains("handler:query:alpha") &&
            state.Events.Contains("post:alpha:query:alpha") &&
            state.Events.Contains($"inner-after:{queryRequestTypeName}") &&
            state.Events.Contains($"outer-after:{queryRequestTypeName}"),
            "Query pipeline markers should all execute.",
            ref checks);
        var queryAsRequest = await sender.SendAsync<string>((IKyrolusRequest<string>)new MediatorProbeQuery("request-query"), cancellationToken).ConfigureAwait(false);
        Require(queryAsRequest == "query:request-query", "Sender should route IKyrolusRequest queries through the query overload.", ref checks);

        var commandResult = await sender.SendAsync<int>((IKyrolusRequest<int>)new MediatorResponseCommand(21), cancellationToken).ConfigureAwait(false);
        Require(commandResult == 42, "Command requests with responses should route correctly.", ref checks);

        var unitResult = await sender.SendAsync<Unit>((IKyrolusRequest<Unit>)new MediatorVoidCommand("void"), cancellationToken).ConfigureAwait(false);
        Require(unitResult == Unit.Value, "Void commands should route as Unit.", ref checks);
        Require(state.VoidCommandCount == 1, "Void command handler should execute once.", ref checks);

        var plainResult = await sender.SendAsync<string>((IKyrolusRequest<string>)new MediatorPlainRequest("plain"), cancellationToken).ConfigureAwait(false);
        Require(plainResult == "plain:plain", "Plain requests should route through the request pipeline.", ref checks);

        var streamValues = new List<int>();
        await foreach (var value in mediator.StreamAsync(new MediatorStreamRequest(3), cancellationToken).ConfigureAwait(false))
        {
            streamValues.Add(value);
        }

        Require(streamValues.SequenceEqual([1, 2, 3]), "Stream handlers should yield the expected sequence.", ref checks);
        Require(state.StreamRequestCount == 1, "Stream request handler should execute once.", ref checks);
        Require(
            ContainsSequence(
                state.Events,
                $"stream-outer-before:{typeof(MediatorStreamRequest).Name}",
                $"stream-inner-before:{typeof(MediatorStreamRequest).Name}",
                $"stream-inner-after:{typeof(MediatorStreamRequest).Name}",
                $"stream-outer-after:{typeof(MediatorStreamRequest).Name}"),
            "Stream pipeline behaviors should execute in deterministic order.",
            ref checks);

        await mediator.PublishAsync(new MediatorSuccessNotification("notify"), cancellationToken).ConfigureAwait(false);
        Require(
            state.NotificationEvents.Contains("success-handler:a:notify") &&
            state.NotificationEvents.Contains("success-handler:b:notify"),
            "Successful notifications should reach all handlers.",
            ref checks);

        var handledResult = await mediator.SendAsync(new MediatorHandledFailureRequest("handled"), cancellationToken).ConfigureAwait(false);
        Require(handledResult == "handled:handled", "Exception handler should be able to recover a response.", ref checks);
        Require(state.HandledExceptionActionCount == 2, "Specific and base exception actions should both execute.", ref checks);
        Require(state.HandledExceptionHandlerCount == 1, "Specific exception handler should execute once.", ref checks);

        await ExpectThrowsAsync<ApplicationException>(
            () => mediator.SendAsync(new MediatorUnhandledFailureRequest("boom"), cancellationToken),
            "Unhandled request exceptions should bubble out.").ConfigureAwait(false);
        checks++;
        Require(state.UnhandledExceptionActionCount == 1, "Unhandled exception actions should execute once.", ref checks);

        await publisher.PublishAsync(new MediatorNoHandlersNotification(), cancellationToken).ConfigureAwait(false);
        checks++;

        var aggregate = await CaptureThrowsAsync<AggregateException>(
            () => publisher.PublishAsync(
                new MediatorFailureNotification("fail"),
                new KyrolusSequentialNotificationPublishStrategy(),
                cancellationToken),
            "Notification failures should aggregate.")
            .ConfigureAwait(false);
        checks++;
        Require(aggregate.InnerExceptions.Count >= 2, "Notification publish should aggregate handler failures.", ref checks);
        Require(state.NotificationEvents.Contains("failure-success:fail"), "Publisher should keep invoking handlers after failures.", ref checks);

        await dispatcher.DispatchCommandAsync(new MediatorFallbackUnitRequest("fallback"), provider, cancellationToken).ConfigureAwait(false);
        Require(state.FallbackUnitRequestCount == 1, "Dispatcher should fall back to IRequestHandler<Unit>.", ref checks);

        await ExpectThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchRequestAsync<string>(new MediatorMissingRequest("missing"), provider, cancellationToken),
            "Missing request handlers should fail.").ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchCommandAsync(new MediatorMissingCommand("missing"), provider, cancellationToken),
            "Missing command handlers should fail.").ConfigureAwait(false);
        checks++;
        ExpectThrows<InvalidOperationException>(
            () => dispatcher.DispatchStreamAsync<int>(new MediatorMissingStreamRequest(1), provider, cancellationToken),
            "Missing stream handlers should fail.",
            ref checks);

        await ExpectThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync<string>((IKyrolusRequest<string>)new MediatorExplicitRequest("explicit"), cancellationToken),
            "Explicit-only handlers should fail reflection dispatch.").ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync<string>((IKyrolusRequest<string>)new MediatorThrowingRequest("throw"), cancellationToken),
            "Sender should unwrap request handler exceptions from reflection dispatch.").ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await foreach (var _ in sender.StreamAsync(new MediatorThrowingStreamRequest(1), cancellationToken).ConfigureAwait(false))
                {
                }
            },
            "Sender should unwrap stream pipeline exceptions from reflection dispatch.").ConfigureAwait(false);
        checks++;

        await ExpectThrowsAsync<ArgumentNullException>(
            () => sender.SendAsync<string>((IKyrolusRequest<string>)null!, cancellationToken),
            "Sender should reject null requests.").ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<ArgumentNullException>(
            () => publisher.PublishAsync((INotification)null!, cancellationToken),
            "Publisher should reject null notifications.").ConfigureAwait(false);
        checks++;

        var parallelStrategy = new KyrolusParallelNotificationPublishStrategy();
        var parallelCount = 0;
        await parallelStrategy.PublishAsync(
            [
                _ =>
                {
                    Interlocked.Increment(ref parallelCount);
                    return Task.CompletedTask;
                },
                _ =>
                {
                    Interlocked.Increment(ref parallelCount);
                    return Task.CompletedTask;
                }
            ],
            cancellationToken).ConfigureAwait(false);
        Require(parallelCount == 2, "Parallel strategy should invoke every delegate.", ref checks);

        var sequentialStrategy = new KyrolusSequentialNotificationPublishStrategy();
        var sequentialOrder = new List<int>();
        await sequentialStrategy.PublishAsync(
            [
                _ =>
                {
                    sequentialOrder.Add(1);
                    return Task.CompletedTask;
                },
                _ =>
                {
                    sequentialOrder.Add(2);
                    return Task.CompletedTask;
                }
            ],
            cancellationToken).ConfigureAwait(false);
        Require(sequentialOrder.SequenceEqual([1, 2]), "Sequential strategy should preserve order.", ref checks);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "mediator-runtime",
            MediatorChecks: checks);
    }

    private static async Task ExpectThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static async Task<TException> CaptureThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    private static bool ContainsSequence(IEnumerable<string> actual, params string[] expected)
    {
        var index = 0;
        foreach (var item in actual)
        {
            if (!string.Equals(item, expected[index], StringComparison.Ordinal))
            {
                continue;
            }

            index++;
            if (index == expected.Length)
            {
                return true;
            }
        }

        return expected.Length == 0;
    }

    internal static async IAsyncEnumerable<TResponse> WrapAsync<TResponse>(
        IAsyncEnumerable<TResponse> source,
        MediatorRuntimeState state,
        string beforeMarker,
        string afterMarker,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        state.Events.Enqueue(beforeMarker);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        state.Events.Enqueue(afterMarker);
    }
}

internal sealed class MediatorRuntimeState
{
    public ConcurrentQueue<string> Events { get; } = new();
    public ConcurrentQueue<string> NotificationEvents { get; } = new();
    public int VoidCommandCount;
    public int StreamRequestCount;
    public int HandledExceptionActionCount;
    public int HandledExceptionHandlerCount;
    public int UnhandledExceptionActionCount;
    public int FallbackUnitRequestCount;
}

internal sealed record MediatorProbeQuery(string Value) : IKyrolusQuery<string>;

internal sealed class MediatorProbeQueryHandler(MediatorRuntimeState state) : IKyrolusQueryHandler<MediatorProbeQuery, string>
{
    public Task<string> Handle(MediatorProbeQuery request, CancellationToken cancellationToken)
    {
        state.Events.Enqueue($"handler:query:{request.Value}");
        return Task.FromResult($"query:{request.Value}");
    }
}

internal sealed class MediatorProbePreProcessor(MediatorRuntimeState state) : IKyrolusRequestPreProcessor<MediatorProbeQuery>
{
    public Task Process(MediatorProbeQuery request, CancellationToken cancellationToken)
    {
        state.Events.Enqueue($"pre:{request.Value}");
        return Task.CompletedTask;
    }
}

internal sealed class MediatorProbePostProcessor(MediatorRuntimeState state) : IKyrolusRequestPostProcessor<MediatorProbeQuery, string>
{
    public Task Process(MediatorProbeQuery request, string response, CancellationToken cancellationToken)
    {
        state.Events.Enqueue($"post:{request.Value}:{response}");
        return Task.CompletedTask;
    }
}

[PipelineOrder(-1500)]
internal sealed class MediatorOuterBehavior<TRequest, TResponse>(MediatorRuntimeState state) : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        state.Events.Enqueue($"outer-before:{typeof(TRequest).Name}");
        var response = await next().ConfigureAwait(false);
        state.Events.Enqueue($"outer-after:{typeof(TRequest).Name}");
        return response;
    }
}

[PipelineOrder(500)]
internal sealed class MediatorInnerBehavior<TRequest, TResponse>(MediatorRuntimeState state) : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        state.Events.Enqueue($"inner-before:{typeof(TRequest).Name}");
        var response = await next().ConfigureAwait(false);
        state.Events.Enqueue($"inner-after:{typeof(TRequest).Name}");
        return response;
    }
}

internal sealed record MediatorResponseCommand(int Value) : IKyrolusCommand<int>;
internal sealed class MediatorResponseCommandHandler : IKyrolusCommandHandler<MediatorResponseCommand, int>
{
    public Task<int> Handle(MediatorResponseCommand request, CancellationToken cancellationToken) => Task.FromResult(request.Value * 2);
}

internal sealed record MediatorVoidCommand(string Value) : IKyrolusCommand;
internal sealed class MediatorVoidCommandHandler(MediatorRuntimeState state) : IKyrolusCommandHandler<MediatorVoidCommand>
{
    public Task Handle(MediatorVoidCommand request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.VoidCommandCount);
        return Task.CompletedTask;
    }
}

internal sealed record MediatorPlainRequest(string Value) : IKyrolusRequest<string>;
internal sealed class MediatorPlainRequestHandler : IKyrolusRequestHandler<MediatorPlainRequest, string>
{
    public Task<string> Handle(MediatorPlainRequest request, CancellationToken cancellationToken) => Task.FromResult($"plain:{request.Value}");
}

internal sealed record MediatorStreamRequest(int Count) : IKyrolusStreamRequest<int>;
internal sealed class MediatorStreamRequestHandler(MediatorRuntimeState state) : IKyrolusStreamRequestHandler<MediatorStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(MediatorStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.StreamRequestCount);
        for (var value = 1; value <= request.Count; value++)
        {
            yield return value;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}

[PipelineOrder(-1500)]
internal sealed class MediatorOuterStreamBehavior<TRequest, TResponse>(MediatorRuntimeState state) : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => RepositoryRuntimeDiagnostics.WrapAsync(
            next(cancellationToken),
            state,
            $"stream-outer-before:{typeof(TRequest).Name}",
            $"stream-outer-after:{typeof(TRequest).Name}",
            cancellationToken);
}

[PipelineOrder(500)]
internal sealed class MediatorInnerStreamBehavior<TRequest, TResponse>(MediatorRuntimeState state) : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => RepositoryRuntimeDiagnostics.WrapAsync(
            next(cancellationToken),
            state,
            $"stream-inner-before:{typeof(TRequest).Name}",
            $"stream-inner-after:{typeof(TRequest).Name}",
            cancellationToken);
}

internal sealed record MediatorThrowingRequest(string Value) : IKyrolusRequest<string>;

internal sealed class MediatorThrowingRequestHandler : IKyrolusRequestHandler<MediatorThrowingRequest, string>
{
    public Task<string> Handle(MediatorThrowingRequest request, CancellationToken cancellationToken)
        => throw new InvalidOperationException($"request:{request.Value}");
}

internal sealed record MediatorThrowingStreamRequest(int Count) : IKyrolusStreamRequest<int>;

internal sealed class MediatorThrowingStreamBehavior : IKyrolusStreamPipelineBehavior<MediatorThrowingStreamRequest, int>
{
    public IAsyncEnumerable<int> Handle(
        MediatorThrowingStreamRequest request,
        StreamHandlerDelegate<int> next,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException($"stream:{request.Count}");
}

internal sealed record MediatorSuccessNotification(string Value) : INotification;
internal sealed class MediatorSuccessNotificationHandlerA(MediatorRuntimeState state) : INotificationHandler<MediatorSuccessNotification>
{
    public Task Handle(MediatorSuccessNotification notification, CancellationToken cancellationToken)
    {
        state.NotificationEvents.Enqueue($"success-handler:a:{notification.Value}");
        return Task.CompletedTask;
    }
}

internal sealed class MediatorSuccessNotificationHandlerB(MediatorRuntimeState state) : INotificationHandler<MediatorSuccessNotification>
{
    public Task Handle(MediatorSuccessNotification notification, CancellationToken cancellationToken)
    {
        state.NotificationEvents.Enqueue($"success-handler:b:{notification.Value}");
        return Task.CompletedTask;
    }
}

internal sealed record MediatorHandledFailureRequest(string Value) : IKyrolusRequest<string>;
internal sealed class MediatorHandledFailureRequestHandler : IKyrolusRequestHandler<MediatorHandledFailureRequest, string>
{
    public Task<string> Handle(MediatorHandledFailureRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException(request.Value);
}

internal sealed class MediatorHandledFailureAction(MediatorRuntimeState state) : IKyrolusRequestExceptionAction<MediatorHandledFailureRequest, InvalidOperationException>
{
    public Task Execute(MediatorHandledFailureRequest request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.HandledExceptionActionCount);
        return Task.CompletedTask;
    }
}

internal sealed class MediatorHandledFailureBaseAction(MediatorRuntimeState state) : IKyrolusRequestExceptionAction<MediatorHandledFailureRequest, Exception>
{
    public Task Execute(MediatorHandledFailureRequest request, Exception exception, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.HandledExceptionActionCount);
        return Task.CompletedTask;
    }
}

internal sealed class MediatorHandledFailureHandler(MediatorRuntimeState state) : IKyrolusRequestExceptionHandler<MediatorHandledFailureRequest, InvalidOperationException, string>
{
    public Task Handle(MediatorHandledFailureRequest request, InvalidOperationException exception, KyrolusRequestExceptionHandlerState<string> stateHolder, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.HandledExceptionHandlerCount);
        stateHolder.SetHandled($"handled:{request.Value}");
        return Task.CompletedTask;
    }
}

internal sealed record MediatorUnhandledFailureRequest(string Value) : IKyrolusRequest<string>;
internal sealed class MediatorUnhandledFailureRequestHandler : IKyrolusRequestHandler<MediatorUnhandledFailureRequest, string>
{
    public Task<string> Handle(MediatorUnhandledFailureRequest request, CancellationToken cancellationToken) => throw new ApplicationException(request.Value);
}

internal sealed class MediatorUnhandledFailureAction(MediatorRuntimeState state) : IKyrolusRequestExceptionAction<MediatorUnhandledFailureRequest, Exception>
{
    public Task Execute(MediatorUnhandledFailureRequest request, Exception exception, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.UnhandledExceptionActionCount);
        return Task.CompletedTask;
    }
}

internal sealed record MediatorNoHandlersNotification : INotification;
internal sealed record MediatorFailureNotification(string Value) : INotification;
internal sealed class MediatorFailureSuccessHandler(MediatorRuntimeState state) : INotificationHandler<MediatorFailureNotification>
{
    public Task Handle(MediatorFailureNotification notification, CancellationToken cancellationToken)
    {
        state.NotificationEvents.Enqueue($"failure-success:{notification.Value}");
        return Task.CompletedTask;
    }
}

internal sealed class MediatorFailureThrowingHandler : INotificationHandler<MediatorFailureNotification>
{
    public Task Handle(MediatorFailureNotification notification, CancellationToken cancellationToken) => throw new InvalidOperationException("Notification handler failed.");
}

internal sealed class MediatorFailureNullTaskHandler : INotificationHandler<MediatorFailureNotification>
{
    public Task Handle(MediatorFailureNotification notification, CancellationToken cancellationToken) => null!;
}

internal sealed record MediatorFallbackUnitRequest(string Value) : IKyrolusRequest<Unit>;
internal sealed class MediatorFallbackUnitRequestHandler(MediatorRuntimeState state) : IKyrolusRequestHandler<MediatorFallbackUnitRequest>
{
    public Task Handle(MediatorFallbackUnitRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref state.FallbackUnitRequestCount);
        return Task.CompletedTask;
    }
}

internal sealed record MediatorMissingRequest(string Value) : IKyrolusRequest<string>;
internal sealed record MediatorMissingCommand(string Value) : IKyrolusCommand;
internal sealed record MediatorMissingStreamRequest(int Count) : IKyrolusStreamRequest<int>;

internal sealed record MediatorExplicitRequest(string Value) : IKyrolusRequest<string>;
internal sealed class MediatorExplicitRequestHandler : IKyrolusRequestHandler<MediatorExplicitRequest, string>
{
    Task<string> IKyrolusRequestHandler<MediatorExplicitRequest, string>.Handle(MediatorExplicitRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"explicit:{request.Value}");
}
