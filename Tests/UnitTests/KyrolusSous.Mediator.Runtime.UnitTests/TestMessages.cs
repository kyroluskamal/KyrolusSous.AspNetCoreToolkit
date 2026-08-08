using System.Collections.Concurrent;

namespace KyrolusSous.Mediator.Tests;

/// <summary>
/// Ordered log shared by the probes in a single test. Instance-scoped rather than static so
/// tests stay independent when xUnit runs classes in parallel.
/// </summary>
public sealed class Recorder
{
    private readonly ConcurrentQueue<string> _entries = new();

    public void Add(string entry) => _entries.Enqueue(entry);

    public IReadOnlyList<string> Entries => [.. _entries];
}

// --- Queries ---

public sealed record Ping(string Message) : IKyrolusQuery<string>;

public sealed class PingHandler(Recorder recorder) : IKyrolusQueryHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken)
    {
        recorder.Add("handler");
        return Task.FromResult($"pong:{request.Message}");
    }
}

// --- Commands with a response ---

public sealed record CreateThing(string Name) : IKyrolusCommand<Guid>;

public sealed class CreateThingHandler : IKyrolusCommandHandler<CreateThing, Guid>
{
    public static readonly Guid KnownId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Task<Guid> Handle(CreateThing request, CancellationToken cancellationToken)
        => Task.FromResult(KnownId);
}

// --- Commands without a response ---

public sealed record DeleteThing(Guid Id) : IKyrolusCommand;

public sealed class DeleteThingHandler(Recorder recorder) : IKyrolusCommandHandler<DeleteThing>
{
    public Task Handle(DeleteThing request, CancellationToken cancellationToken)
    {
        recorder.Add($"deleted:{request.Id}");
        return Task.CompletedTask;
    }
}

// --- A handler that serves two different requests (regression for the cache-key bug) ---

public sealed record FirstRequest(int Value) : IKyrolusRequest<string>;

public sealed record SecondRequest(int Value) : IKyrolusRequest<string>;

/// <summary>
/// One class implementing two request handler interfaces. The old cache keyed only on the
/// handler type, so dispatching the second request reused the first request's Handle method.
/// </summary>
public sealed class DualRequestHandler :
    IKyrolusRequestHandler<FirstRequest, string>,
    IKyrolusRequestHandler<SecondRequest, string>
{
    public Task<string> Handle(FirstRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"first:{request.Value}");

    public Task<string> Handle(SecondRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"second:{request.Value}");
}

// --- Notifications ---

public sealed record SomethingHappened(string What) : INotification;

public sealed record SomethingElseHappened(string What) : INotification;

public sealed class RecordingNotificationHandler(Recorder recorder) : INotificationHandler<SomethingHappened>
{
    public Task Handle(SomethingHappened notification, CancellationToken cancellationToken)
    {
        recorder.Add($"first:{notification.What}");
        return Task.CompletedTask;
    }
}

public sealed class SecondRecordingNotificationHandler(Recorder recorder) : INotificationHandler<SomethingHappened>
{
    public Task Handle(SomethingHappened notification, CancellationToken cancellationToken)
    {
        recorder.Add($"second:{notification.What}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// One class handling two notification types. Regression probe for the publisher's cache key.
/// </summary>
public sealed class DualNotificationHandler(Recorder recorder) :
    INotificationHandler<SomethingHappened>,
    INotificationHandler<SomethingElseHappened>
{
    public Task Handle(SomethingHappened notification, CancellationToken cancellationToken)
    {
        recorder.Add($"happened:{notification.What}");
        return Task.CompletedTask;
    }

    public Task Handle(SomethingElseHappened notification, CancellationToken cancellationToken)
    {
        recorder.Add($"else:{notification.What}");
        return Task.CompletedTask;
    }
}

public sealed class ThrowingNotificationHandler : INotificationHandler<SomethingHappened>
{
    public Task Handle(SomethingHappened notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("handler-one-failed");
}

public sealed class SecondThrowingNotificationHandler : INotificationHandler<SomethingHappened>
{
    public Task Handle(SomethingHappened notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("handler-two-failed");
}

// --- Streaming ---

public sealed record CountTo(int Max) : IKyrolusStreamRequest<int>;

public sealed class CountToHandler : IKyrolusStreamRequestHandler<CountTo, int>
{
    public async IAsyncEnumerable<int> Handle(
        CountTo request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= request.Max; i++)
        {
            // Cancellation is cooperative: a stream handler has to check the token itself.
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return i;
        }
    }
}

// --- Behaviors ---

public sealed class UnorderedBehaviorA<TRequest, TResponse>(Recorder recorder)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        recorder.Add("A");
        return next(cancellationToken);
    }
}

public sealed class UnorderedBehaviorB<TRequest, TResponse>(Recorder recorder)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        recorder.Add("B");
        return next(cancellationToken);
    }
}

public sealed class UnorderedBehaviorC<TRequest, TResponse>(Recorder recorder)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        recorder.Add("C");
        return next(cancellationToken);
    }
}

[PipelineOrder(-50)]
public sealed class EarlyBehavior<TRequest, TResponse>(Recorder recorder)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        recorder.Add("early");
        return next(cancellationToken);
    }
}

[PipelineOrder(50)]
public sealed class LateBehavior<TRequest, TResponse>(Recorder recorder)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        recorder.Add("late");
        return next(cancellationToken);
    }
}

/// <summary>Calls <c>next()</c> with no argument, exercising the delegate's optional token.</summary>
public sealed class NoArgNextBehavior<TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => next();
}

/// <summary>Short-circuits the pipeline without ever calling the handler.</summary>
public sealed class ShortCircuitBehavior(Recorder recorder) : IKyrolusPipelineBehavior<Ping, string>
{
    public Task<string> Handle(Ping request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        recorder.Add("short-circuit");
        return Task.FromResult("cached");
    }
}

// --- Pre / post processors ---

public sealed class PingPreProcessor(Recorder recorder) : IKyrolusRequestPreProcessor<Ping>
{
    public Task Process(Ping request, CancellationToken cancellationToken)
    {
        recorder.Add("pre");
        return Task.CompletedTask;
    }
}

public sealed class PingPostProcessor(Recorder recorder) : IKyrolusRequestPostProcessor<Ping, string>
{
    public Task Process(Ping request, string response, CancellationToken cancellationToken)
    {
        recorder.Add($"post:{response}");
        return Task.CompletedTask;
    }
}

// --- Exception handling ---

public sealed record Explode(string Mode) : IKyrolusQuery<string>;

public sealed class ExplodeHandler : IKyrolusQueryHandler<Explode, string>
{
    public Task<string> Handle(Explode request, CancellationToken cancellationToken)
        => throw new InvalidOperationException($"boom:{request.Mode}");
}

public sealed class ExplodeExceptionAction(Recorder recorder)
    : IKyrolusRequestExceptionAction<Explode, InvalidOperationException>
{
    public Task Execute(Explode request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        recorder.Add($"action:{exception.Message}");
        return Task.CompletedTask;
    }
}

/// <summary>An action that itself throws. Must not stop the other actions or replace the original exception.</summary>
public sealed class FailingExceptionAction
    : IKyrolusRequestExceptionAction<Explode, InvalidOperationException>
{
    public Task Execute(Explode request, InvalidOperationException exception, CancellationToken cancellationToken)
        => throw new NotSupportedException("telemetry-backend-is-down");
}

/// <summary>Registered after the failing one, to prove the failure did not abort the sequence.</summary>
public sealed class SecondExplodeExceptionAction(Recorder recorder)
    : IKyrolusRequestExceptionAction<Explode, InvalidOperationException>
{
    public Task Execute(Explode request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        recorder.Add("second-action-ran");
        return Task.CompletedTask;
    }
}

/// <summary>Registered against the base type, to prove the whole inheritance chain still runs.</summary>
public sealed class BaseTypeExceptionAction(Recorder recorder)
    : IKyrolusRequestExceptionAction<Explode, Exception>
{
    public Task Execute(Explode request, Exception exception, CancellationToken cancellationToken)
    {
        recorder.Add("base-type-action-ran");
        return Task.CompletedTask;
    }
}

public sealed class ExplodeExceptionHandler(Recorder recorder)
    : IKyrolusRequestExceptionHandler<Explode, InvalidOperationException, string>
{
    public Task Handle(
        Explode request,
        InvalidOperationException exception,
        KyrolusRequestExceptionHandlerState<string> state,
        CancellationToken cancellationToken)
    {
        recorder.Add("recovered");
        if (request.Mode == "recover")
        {
            state.SetHandled("recovered-response");
        }

        return Task.CompletedTask;
    }
}

// --- Duplicate handler probes ---

public sealed record Ambiguous(int Value) : IKyrolusQuery<int>;

public sealed class AmbiguousHandlerOne : IKyrolusQueryHandler<Ambiguous, int>
{
    public Task<int> Handle(Ambiguous request, CancellationToken cancellationToken) => Task.FromResult(1);
}

public sealed class AmbiguousHandlerTwo : IKyrolusQueryHandler<Ambiguous, int>
{
    public Task<int> Handle(Ambiguous request, CancellationToken cancellationToken) => Task.FromResult(2);
}
