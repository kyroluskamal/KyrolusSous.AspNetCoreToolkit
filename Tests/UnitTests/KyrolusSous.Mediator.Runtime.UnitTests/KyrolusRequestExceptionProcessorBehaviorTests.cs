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
