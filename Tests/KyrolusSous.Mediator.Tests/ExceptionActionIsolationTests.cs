using Microsoft.Extensions.Logging;

namespace KyrolusSous.Mediator.Tests;

/// <summary>
/// An exception action is an independent side effect. One of them failing must not skip the
/// others, and must never replace the exception the request actually failed with.
/// </summary>
public sealed class ExceptionActionIsolationTests
{
    private static IServiceCollection WithFailingAction(Recorder recorder)
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddTransient<IKyrolusQueryHandler<Explode, string>, ExplodeHandler>();

        // Registration order matters: the failing one sits in the middle.
        services.AddTransient<IKyrolusRequestExceptionAction<Explode, InvalidOperationException>, ExplodeExceptionAction>();
        services.AddTransient<IKyrolusRequestExceptionAction<Explode, InvalidOperationException>, FailingExceptionAction>();
        services.AddTransient<IKyrolusRequestExceptionAction<Explode, InvalidOperationException>, SecondExplodeExceptionAction>();

        return services;
    }

    [Fact]
    public async Task A_failing_action_does_not_replace_the_original_exception()
    {
        await using var provider = WithFailingAction(new Recorder()).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => mediator.SendAsync(new Explode("rethrow")));

        // Not NotSupportedException from the failing action.
        exception.Message.ShouldBe("boom:rethrow");
    }

    [Fact]
    public async Task A_failing_action_does_not_stop_the_actions_after_it()
    {
        var recorder = new Recorder();
        await using var provider = WithFailingAction(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("rethrow")));

        recorder.Entries.ShouldContain("action:boom:rethrow");   // ran before the failure
        recorder.Entries.ShouldContain("second-action-ran");     // ran after it
    }

    [Fact]
    public async Task A_failing_action_does_not_stop_actions_registered_on_a_base_type()
    {
        var recorder = new Recorder();
        var services = WithFailingAction(recorder);
        services.AddTransient<IKyrolusRequestExceptionAction<Explode, Exception>, BaseTypeExceptionAction>();

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("rethrow")));

        recorder.Entries.ShouldContain("base-type-action-ran");
    }

    [Fact]
    public async Task A_failing_action_does_not_stop_a_handler_from_recovering()
    {
        var recorder = new Recorder();
        var services = WithFailingAction(recorder);
        services.AddTransient<IKyrolusRequestExceptionHandler<Explode, InvalidOperationException, string>, ExplodeExceptionHandler>();

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Explode("recover"))).ShouldBe("recovered-response");
    }

    [Fact]
    public async Task A_failing_action_is_reported_through_the_logger()
    {
        var recorder = new Recorder();
        var sink = new CapturingLoggerProvider();
        var services = WithFailingAction(recorder);
        services.AddLogging(builder => builder.AddProvider(sink).SetMinimumLevel(LogLevel.Trace));

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("rethrow")));

        var entry = sink.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Message.ShouldContain(nameof(FailingExceptionAction));
        entry.Exception.ShouldBeOfType<NotSupportedException>()
            .Message.ShouldBe("telemetry-backend-is-down");   // unwrapped, not TargetInvocationException
    }

    [Fact]
    public async Task Everything_still_works_when_no_logging_is_registered()
    {
        // The logger is resolved optionally - an app with no logging must not break.
        await using var provider = WithFailingAction(new Recorder()).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        provider.GetService<ILoggerFactory>().ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("rethrow")));
    }
}

// --- A minimal logger that records what it was told ---

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

    public void Dispose() { }

    private sealed class CapturingLogger(System.Collections.Concurrent.ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}
