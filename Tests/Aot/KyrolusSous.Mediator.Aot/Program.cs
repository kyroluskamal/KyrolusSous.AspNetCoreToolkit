using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Mediator.Runtime.Config;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Mediator.Aot;

// Every message below answers with a value type. That is deliberate: a reference-type response
// survives NativeAOT on shared generics whatever the library does, so a suite of string-returning
// requests would pass while the library was still broken.

internal static class Program
{
    private static async Task<int> Main()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();

        // The generated registrations. Nothing here scans an assembly, so nothing depends on
        // metadata that trimming is free to remove.
        services.AddKyrolusMediatorHandlers();
        services.AddKyrolusMediatorNotificationHandlers();
        services.AddKyrolusMediatorGeneratedDispatcher();

        services.AddTransient<IKyrolusRequestExceptionAction<Explode, Exception>, RecordExplosion>();
        services.AddTransient<IKyrolusRequestExceptionHandler<Explode, InvalidOperationException, int>, RecoverFromExplosion>();

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IKyrolusMediatorSender>();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        var failures = 0;

        // Query -> int. The instantiation that used to fail: RequestPipelineWrapperImpl<GetCount, int>.
        failures += Check("query returning int", await sender.SendAsync(new GetCount(20)), 42);

        // Command with no response, dispatched as Unit.
        await sender.SendAsync(new Ping());
        failures += Check("command returning nothing", PingHandler.Calls, 1);

        // Command -> Guid, a struct that is not a primitive.
        var id = await sender.SendAsync(new CreateThing());
        failures += Check("command returning Guid", id != Guid.Empty, true);

        // Stream -> int.
        var total = 0;
        await foreach (var tick in sender.StreamAsync(new Ticks(4)))
        {
            total += tick;
        }

        failures += Check("stream of int", total, 10);

        // Notification with two handlers.
        await publisher.PublishAsync(new Pinged(7));
        failures += Check("notification reached both handlers", PingedHandlers.Calls, 2);

        // Exception action runs, then the handler recovers with an int response.
        failures += Check("recovered response", await sender.SendAsync(new Explode()), -1);
        failures += Check("exception action ran", RecordExplosion.Calls, 1);

        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        return failures;
    }

    private static int Check<T>(string what, T actual, T expected)
    {
        var ok = EqualityComparer<T>.Default.Equals(actual, expected);
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}: expected {expected}, got {actual}");
        return ok ? 0 : 1;
    }
}

// --- Query returning a primitive value type ---

public sealed record GetCount(int Seed) : IKyrolusQuery<int>;

public sealed class GetCountHandler : IKyrolusQueryHandler<GetCount, int>
{
    public Task<int> Handle(GetCount request, CancellationToken cancellationToken)
        => Task.FromResult(request.Seed + 22);
}

// --- Command with no response ---

public sealed record Ping : IKyrolusCommand;

public sealed class PingHandler : IKyrolusCommandHandler<Ping>
{
    public static int Calls;

    public Task Handle(Ping request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

// --- Command returning a non-primitive struct ---

public sealed record CreateThing : IKyrolusCommand<Guid>;

public sealed class CreateThingHandler : IKyrolusCommandHandler<CreateThing, Guid>
{
    public Task<Guid> Handle(CreateThing request, CancellationToken cancellationToken)
        => Task.FromResult(Guid.NewGuid());
}

// --- Stream of a value type ---

public sealed record Ticks(int Count) : IKyrolusStreamRequest<int>;

public sealed class TicksHandler : IKyrolusStreamRequestHandler<Ticks, int>
{
    public async IAsyncEnumerable<int> Handle(
        Ticks request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= request.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

// --- Notification with more than one handler ---

public sealed record Pinged(int Value) : IKyrolusNotification;

public static class PingedHandlers
{
    public static int Calls;
}

public sealed class FirstPingedHandler : IKyrolusNotificationHandler<Pinged>
{
    public Task Handle(Pinged notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref PingedHandlers.Calls);
        return Task.CompletedTask;
    }
}

public sealed class SecondPingedHandler : IKyrolusNotificationHandler<Pinged>
{
    public Task Handle(Pinged notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref PingedHandlers.Calls);
        return Task.CompletedTask;
    }
}

// --- Exception action and handler, over a value-type response ---

public sealed record Explode : IKyrolusQuery<int>;

public sealed class ExplodeHandler : IKyrolusQueryHandler<Explode, int>
{
    public Task<int> Handle(Explode request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("boom");
}

public sealed class RecordExplosion : IKyrolusRequestExceptionAction<Explode, Exception>
{
    public static int Calls;

    public Task Execute(Explode request, Exception exception, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

public sealed class RecoverFromExplosion : IKyrolusRequestExceptionHandler<Explode, InvalidOperationException, int>
{
    public Task Handle(
        Explode request,
        InvalidOperationException exception,
        KyrolusRequestExceptionHandlerState<int> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(-1);
        return Task.CompletedTask;
    }
}
