using Microsoft.Extensions.Logging;

namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorLoggingBehaviorTests
{
    [Fact(DisplayName = "KyrolusMediatorLoggingBehavior logs a starting entry and a completed entry for a succeeded request")]
    public async Task LogsSucceededRequest()
    {
        var recorder = new Recorder();
        var loggerFactory = new RecordingLoggerFactory();
        var services = TestHost.Standard(recorder, configuration => configuration.AddKyrolusMediatorLogging());
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("hi"));

        loggerFactory.Entries.ShouldContain(e => e.Level == LogLevel.Debug && e.Message.Contains(nameof(Ping)) && e.Message.Contains("starting"));
        loggerFactory.Entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message.Contains(nameof(Ping)) && e.Message.Contains("completed"));
        loggerFactory.Entries.ShouldNotContain(e => e.Level == LogLevel.Warning);
    }

    [Fact(DisplayName = "KyrolusMediatorLoggingBehavior logs a warning with the exception for a faulted request and still rethrows it")]
    public async Task LogsFaultedRequest_AndRethrows()
    {
        var recorder = new Recorder();
        var loggerFactory = new RecordingLoggerFactory();
        var services = TestHost.Standard(recorder, configuration => configuration.AddKyrolusMediatorLogging());
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddTransient<IKyrolusQueryHandler<Explode, string>, ExplodeHandler>();
        services.AddTransient<IKyrolusRequestHandler<Explode, string>, ExplodeHandler>();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("boom")));

        var warning = loggerFactory.Entries.Single(e => e.Level == LogLevel.Warning);
        warning.Message.ShouldContain(nameof(Explode));
        warning.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact(DisplayName = "KyrolusMediatorLoggingBehavior does nothing when no ILoggerFactory is registered")]
    public async Task NoOp_WhenNoLoggerFactoryRegistered()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder, configuration =>
            configuration.AddKyrolusMediatorLogging()).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        // Only assertion that matters: this does not throw for lack of a logger.
        var response = await mediator.SendAsync(new Ping("hi"));
        response.ShouldBe("pong:hi");
    }

    [Fact(DisplayName = "AddKyrolusMediatorLogging is equivalent to AddOpenBehavior(typeof(KyrolusMediatorLoggingBehavior<,>))")]
    public void AddKyrolusMediatorLogging_RegistersTheOpenBehavior()
    {
        var configuration = new KyrolusMediatorConfiguration();

        configuration.AddKyrolusMediatorLogging();

        configuration.OpenBehaviors.ShouldContain(b =>
            b.Service == typeof(IKyrolusPipelineBehavior<,>) && b.Implementation == typeof(KyrolusMediatorLoggingBehavior<,>));
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

        public void Dispose() { }

        private sealed class RecordingLogger(RecordingLoggerFactory owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => owner.Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
