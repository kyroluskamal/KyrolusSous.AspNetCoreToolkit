using Microsoft.Extensions.Logging;

namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusRequestExceptionProcessorBehaviorTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    [Fact(DisplayName = "Exception handler can handle exception and supply a replacement response")]
    public async Task Exception_handler_can_supply_a_replacement_response()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).WithExplodingQuery().BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Explode("recover"))).ShouldBe("recovered-response");
        recorder.Entries.ShouldContain("action:boom:recover");
    }

    [Fact(DisplayName = "Unhandled exception in handler is rethrown after exception actions complete")]
    public async Task Unhandled_exception_is_rethrown_after_the_actions_run()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).WithExplodingQuery().BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("rethrow")));
        recorder.Entries.ShouldContain("action:boom:rethrow");
    }

    [Fact(DisplayName = "Exception action failure is isolated and does not stop remaining actions from executing")]
    public async Task Action_failing_does_not_stop_the_remaining_actions_from_running()
    {
        var recorder = new Recorder();
        await using var provider = WithFailingAction(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("x")));

        recorder.Entries.ShouldContain("action-2");
    }

    [Fact(DisplayName = "Exception action failure does not swallow or alter original request exception")]
    public async Task Action_failing_does_not_replace_the_original_exception()
    {
        var recorder = new Recorder();
        await using var provider = WithFailingAction(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("original")));

        exception.Message.ShouldBe("boom:original");
    }

    [Fact(DisplayName = "Constructor throws ArgumentNullException when serviceProvider is null")]
    public void Constructor_throws_ArgumentNullException_when_serviceProvider_is_null()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new KyrolusRequestExceptionProcessorBehavior<Ping, string>(null!));

        exception.ParamName.ShouldBe("serviceProvider");
    }

    [Fact(DisplayName = "Processor behavior handles missing dispatch source gracefully when pipeline throws")]
    public async Task Processor_behavior_handles_missing_dispatch_source_gracefully()
    {
        var services = new ServiceCollection();
        await using var sp = services.BuildServiceProvider();

        var behavior = new KyrolusRequestExceptionProcessorBehavior<Ping, string>(sp);

        await Should.ThrowAsync<InvalidOperationException>(
            () => behavior.Handle(new Ping("test"), _ => throw new InvalidOperationException("boom"), CancellationToken.None));
    }

    [Fact(DisplayName = "Processor behavior logs action failure and swallows logger failure if logger throws")]
    public async Task Processor_behavior_swallows_logger_failure_when_logger_factory_throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratorIntegration.IKyrolusRequestExceptionDispatchSource>(new FailingDispatchSource());
        services.AddSingleton<ILoggerFactory>(new ExplodingLoggerFactory());
        await using var sp = services.BuildServiceProvider();

        var behavior = new KyrolusRequestExceptionProcessorBehavior<Ping, string>(sp);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => behavior.Handle(new Ping("test"), _ => throw new InvalidOperationException("original-error"), CancellationToken.None));

        ex.Message.ShouldBe("original-error");
    }

    private static IServiceCollection WithFailingAction(Recorder recorder)
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<IKyrolusQueryHandler<Explode, string>, ExplodeHandler>();
        services.AddTransient<IKyrolusRequestExceptionAction<Explode, InvalidOperationException>, FailingAction>();
        services.AddTransient<IKyrolusRequestExceptionAction<Explode, InvalidOperationException>, SecondAction>();
        return services;
    }
}

public sealed class FailingDispatchSource : GeneratorIntegration.IKyrolusRequestExceptionDispatchSource
{
    public IReadOnlyList<(Type ActionType, Func<CancellationToken, Task> Invoke)>? CreateActionInvocations(Type requestType, Type exceptionType, object request, Exception exception, IServiceProvider serviceProvider)
    {
        if (exceptionType == typeof(InvalidOperationException))
        {
            return [(typeof(FailingAction), _ => throw new InvalidOperationException("action-failed"))];
        }
        return null;
    }

    public IReadOnlyList<Func<CancellationToken, Task>>? CreateHandlerInvocations(Type requestType, Type exceptionType, Type responseType, object request, Exception exception, object state, IServiceProvider serviceProvider)
        => null;
}

public sealed class ExplodingLoggerFactory : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider) { }
    public ILogger CreateLogger(string categoryName) => throw new InvalidOperationException("Logging is broken!");
    public void Dispose() { }
}

public sealed class FailingAction : IKyrolusRequestExceptionAction<Explode, InvalidOperationException>
{
    public Task Execute(Explode request, InvalidOperationException exception, CancellationToken cancellationToken)
        => throw new InvalidOperationException("action-failed");
}

public sealed class SecondAction(Recorder recorder) : IKyrolusRequestExceptionAction<Explode, InvalidOperationException>
{
    public Task Execute(Explode request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        recorder.Add("action-2");
        return Task.CompletedTask;
    }
}
