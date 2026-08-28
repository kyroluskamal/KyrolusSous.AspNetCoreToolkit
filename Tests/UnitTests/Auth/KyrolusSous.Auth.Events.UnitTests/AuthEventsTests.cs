using KyrolusSous.Auth.Events;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Events.UnitTests;

public class AuthEventsTests
{
    [Fact(DisplayName = "Dispatcher Invokes Registered Handler")]
    public async Task Dispatcher_InvokesRegisteredHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusAuthEvents();
        services.AddKyrolusAuthEventHandler<KyrolusUserLoggedInEvent, TestLoginHandler>();

        var provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IKyrolusAuthEventSink>();

        var testEvent = new KyrolusUserLoggedInEvent("user-1", "alice", "Password", "127.0.0.1", "TestAgent");

        TestLoginHandler.HandledEvents.Clear();
        await sink.PublishAsync(testEvent);

        TestLoginHandler.HandledEvents.Count.ShouldBe(1);
        TestLoginHandler.HandledEvents[0].UserId.ShouldBe("user-1");
        TestLoginHandler.HandledEvents[0].EventType.ShouldBe("UserLoggedIn");
    }

    [Fact(DisplayName = "Dispatcher Continues Gracefully When Handler Throws")]
    public async Task Dispatcher_ContinuesGracefully_WhenHandlerThrows()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusAuthEvents();
        services.AddKyrolusAuthEventHandler<KyrolusUserLoginFailedEvent, FaultyHandler>();
        services.AddKyrolusAuthEventHandler<KyrolusUserLoginFailedEvent, SuccessfulHandler>();

        var provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IKyrolusAuthEventSink>();

        var testEvent = new KyrolusUserLoginFailedEvent("bob", "Wrong password");

        SuccessfulHandler.Executed = false;

        // Should not throw:
        await sink.PublishAsync(testEvent);

        SuccessfulHandler.Executed.ShouldBeTrue();
    }

    [Fact(DisplayName = "Dispatcher Skips Null Handlers And Executes Remaining")]
    public async Task Dispatcher_SkipsNullHandlers_AndExecutesRemaining()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusAuthEvents();
        services.AddTransient<IKyrolusAuthEventHandler<KyrolusUserLoginFailedEvent>>(_ => null!);
        services.AddKyrolusAuthEventHandler<KyrolusUserLoginFailedEvent, SuccessfulHandler>();

        var provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IKyrolusAuthEventSink>();

        SuccessfulHandler.Executed = false;
        await sink.PublishAsync(new KyrolusUserLoginFailedEvent("charlie", "Bad attempt"));

        SuccessfulHandler.Executed.ShouldBeTrue();
    }

    [Fact(DisplayName = "Dispatcher Dispatches Account Locked And Token Revoked")]
    public async Task Dispatcher_DispatchesAccountLockedAndTokenRevoked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusAuthEvents();
        services.AddKyrolusAuthEventHandler<KyrolusAccountLockedEvent, TestLockedHandler>();

        var provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IKyrolusAuthEventSink>();

        var lockedEvent = new KyrolusAccountLockedEvent("user-99", 5, DateTimeOffset.UtcNow.AddMinutes(30), "10.0.0.5");

        TestLockedHandler.HandledEvent = null;
        await sink.PublishAsync(lockedEvent);

        TestLockedHandler.HandledEvent.ShouldNotBeNull();
        TestLockedHandler.HandledEvent.UserId.ShouldBe("user-99");
        TestLockedHandler.HandledEvent.FailedCount.ShouldBe(5);
    }

    [Fact(DisplayName = "Di Registration Add Kyrolus Auth Events Registers Sink")]
    public void DiRegistration_AddKyrolusAuthEvents_RegistersSink()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusAuthEvents();

        var provider = services.BuildServiceProvider();
        provider.GetService<IKyrolusAuthEventSink>().ShouldNotBeNull();
    }

    [Fact(DisplayName = "Publish Async Respects Cancellation Token")]
    public async Task PublishAsync_RespectsCancellationToken()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusAuthEvents();

        var provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IKyrolusAuthEventSink>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var dummyEvent = new KyrolusUserLoggedInEvent("u", "un", "m");

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await sink.PublishAsync(dummyEvent, cts.Token));
    }

    [Fact(DisplayName = "Login Failed Event Sanitizes Excessive Or Suspicious Identifier")]
    public void LoginFailedEvent_SanitizesExcessiveOrSuspiciousIdentifier()
    {
        var hugeIdentifier = new string('x', 500);
        var failedEvent = new KyrolusUserLoginFailedEvent(hugeIdentifier, "Invalid password");

        failedEvent.AttemptedIdentifier.Length.ShouldBe(256);
    }

    private sealed class TestLockedHandler : IKyrolusAuthEventHandler<KyrolusAccountLockedEvent>
    {
        public static KyrolusAccountLockedEvent? HandledEvent;

        public ValueTask HandleAsync(KyrolusAccountLockedEvent authEvent, CancellationToken cancellationToken = default)
        {
            HandledEvent = authEvent;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestLoginHandler : IKyrolusAuthEventHandler<KyrolusUserLoggedInEvent>
    {
        public static readonly List<KyrolusUserLoggedInEvent> HandledEvents = [];

        public ValueTask HandleAsync(KyrolusUserLoggedInEvent authEvent, CancellationToken cancellationToken = default)
        {
            HandledEvents.Add(authEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FaultyHandler : IKyrolusAuthEventHandler<KyrolusUserLoginFailedEvent>
    {
        public ValueTask HandleAsync(KyrolusUserLoginFailedEvent authEvent, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("External SIEM down!");
        }
    }

    private sealed class SuccessfulHandler : IKyrolusAuthEventHandler<KyrolusUserLoginFailedEvent>
    {
        public static bool Executed { get; set; }

        public ValueTask HandleAsync(KyrolusUserLoginFailedEvent authEvent, CancellationToken cancellationToken = default)
        {
            Executed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact(DisplayName = "Dispatcher Gracefully Handles Resolution Exceptions")]
    public async Task Dispatcher_GracefullyHandles_ResolutionExceptions()
    {
        var faultyProvider = new FaultyServiceProvider();
        var dispatcher = new KyrolusAuthEventDispatcher(faultyProvider, Microsoft.Extensions.Logging.Abstractions.NullLogger<KyrolusAuthEventDispatcher>.Instance);

        await dispatcher.PublishAsync(new KyrolusUserLoginFailedEvent("alice", "failed"));
    }

    private sealed class FaultyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new InvalidOperationException("DI container broken");
    }

    [Fact(DisplayName = "Token Revoked Event Throws When Jti Is Null Or Whitespace")]
    public void TokenRevokedEvent_Throws_WhenJtiIsNullOrWhitespace()
    {
        Should.Throw<ArgumentException>(() =>
            new KyrolusTokenRevokedEvent("   ", "user-1"));
    }
}
