using System.Collections.Concurrent;
using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.Mediator.Runtime.IntegrationTests;

public sealed class IntegrationRecorder
{
    private readonly ConcurrentQueue<string> _entries = new();
    public void Add(string entry) => _entries.Enqueue(entry);
    public IReadOnlyList<string> Entries => [.. _entries];
}

// --- Query ---
public sealed record GetSampleCount(int Value) : IKyrolusQuery<int>;

public sealed class GetSampleCountHandler(IntegrationRecorder recorder) : IKyrolusQueryHandler<GetSampleCount, int>
{
    public Task<int> Handle(GetSampleCount request, CancellationToken cancellationToken)
    {
        recorder.Add($"Query:{request.Value}");
        return Task.FromResult(request.Value * 2);
    }
}

// --- Command ---
public sealed record ExecuteSamplePing : IKyrolusCommand;

public sealed class ExecuteSamplePingHandler(IntegrationRecorder recorder) : IKyrolusCommandHandler<ExecuteSamplePing>
{
    public Task Handle(ExecuteSamplePing request, CancellationToken cancellationToken)
    {
        recorder.Add("CommandHandled");
        return Task.CompletedTask;
    }
}

// --- Stream ---
public sealed record StreamSampleNumbers(int Count) : IKyrolusStreamRequest<int>;

public sealed class StreamSampleNumbersHandler : IKyrolusStreamRequestHandler<StreamSampleNumbers, int>
{
    public async IAsyncEnumerable<int> Handle(StreamSampleNumbers request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

// --- Notification ---
public sealed record SampleEvent(string Message) : INotification;

public sealed class FirstSampleEventHandler(IntegrationRecorder recorder) : INotificationHandler<SampleEvent>
{
    public Task Handle(SampleEvent notification, CancellationToken cancellationToken)
    {
        recorder.Add($"Handler1:{notification.Message}");
        return Task.CompletedTask;
    }
}

public sealed class SecondSampleEventHandler(IntegrationRecorder recorder) : INotificationHandler<SampleEvent>
{
    public Task Handle(SampleEvent notification, CancellationToken cancellationToken)
    {
        recorder.Add($"Handler2:{notification.Message}");
        return Task.CompletedTask;
    }
}

// --- Exception Handling ---
public sealed record ExplodingRequest : IKyrolusQuery<string>;

public sealed class ExplodingRequestHandler : IKyrolusQueryHandler<ExplodingRequest, string>
{
    public Task<string> Handle(ExplodingRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Sample explosion");
    }
}

public sealed class RecordExplosionAction(IntegrationRecorder recorder) : IKyrolusRequestExceptionAction<ExplodingRequest, InvalidOperationException>
{
    public Task Execute(ExplodingRequest request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        recorder.Add($"ActionRecorded:{exception.Message}");
        return Task.CompletedTask;
    }
}

public sealed class RecoverExplosionHandler(IntegrationRecorder recorder) : IKyrolusRequestExceptionHandler<ExplodingRequest, InvalidOperationException, string>
{
    public Task Handle(ExplodingRequest request, InvalidOperationException exception, KyrolusRequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
    {
        recorder.Add("HandlerRecovered");
        state.SetHandled("recovered_fallback");
        return Task.CompletedTask;
    }
}
