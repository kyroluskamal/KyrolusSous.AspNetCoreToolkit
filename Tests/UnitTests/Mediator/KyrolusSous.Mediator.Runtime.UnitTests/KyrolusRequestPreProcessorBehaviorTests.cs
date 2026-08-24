namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusRequestPreProcessorBehaviorTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    [Fact(DisplayName = "Pipeline behavior can short-circuit request without invoking handler")]
    public async Task Behavior_can_short_circuit_without_calling_the_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder, configuration =>
            configuration.AddBehavior<ShortCircuitBehavior>());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Ping("hi"))).ShouldBe("cached");
        recorder.Entries.ShouldNotContain("handler");
    }

    [Fact(DisplayName = "Pipeline behavior calling next delegate without token routes to handler")]
    public async Task Behavior_calling_next_without_a_token_still_reaches_the_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder, configuration =>
            configuration.AddOpenBehavior(typeof(NoArgNextBehavior<,>)));
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Ping("hi"))).ShouldBe("pong:hi");
    }

    [Fact(DisplayName = "Pre and post processors execute around request handler in correct order")]
    public async Task Pre_and_post_processors_run_around_the_handler()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).WithPingProcessors().BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("hi"));

        var relevant = recorder.Entries.Where(e => e is "pre" or "handler" || e.StartsWith("post:")).ToArray();
        relevant.ShouldBe(["pre", "handler", "post:pong:hi"]);
    }

    [Fact(DisplayName = "PreProcessor respects cancellation token before calling handler")]
    public async Task PreProcessor_throws_when_cancellation_requested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).WithPingProcessors().BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<OperationCanceledException>(() => mediator.SendAsync(new Ping("cancelled"), cts.Token));
        recorder.Entries.ShouldNotContain("handler");
    }
}
