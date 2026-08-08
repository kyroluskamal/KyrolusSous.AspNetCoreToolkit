namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorSenderTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    [Fact(DisplayName = "SendAsync query routes request to its registered query handler")]
    public async Task Query_reaches_its_handler()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Ping("hi"))).ShouldBe("pong:hi");
    }

    [Fact(DisplayName = "SendAsync command with response routes to its registered command handler")]
    public async Task Command_with_response_reaches_its_handler()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new CreateThing("x"))).ShouldBe(CreateThingHandler.KnownId);
    }

    [Fact(DisplayName = "SendAsync command without response routes to its registered command handler")]
    public async Task Command_without_response_reaches_its_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder);
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        var id = Guid.NewGuid();

        await mediator.SendAsync(new DeleteThing(id));

        recorder.Entries.ShouldContain($"deleted:{id}");
    }

    [Fact(DisplayName = "SendAsync request interface overload routes correctly to query handler")]
    public async Task Query_sent_through_the_request_overload_still_routes_to_the_query_handler()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        IKyrolusRequest<string> asRequest = new Ping("via-request");

        (await mediator.SendAsync(asRequest)).ShouldBe("pong:via-request");
    }

    [Fact(DisplayName = "SendAsync missing handler throws InvalidOperationException with request name")]
    public async Task Missing_handler_is_reported_with_the_request_name()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => mediator.SendAsync(new Unhandled()));

        exception.Message.ShouldContain(nameof(Unhandled));
    }

    [Fact(DisplayName = "SendAsync null request parameter throws ArgumentNullException")]
    public async Task Null_request_is_rejected()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => mediator.SendAsync((IKyrolusQuery<string>)null!));
    }

    [Fact(DisplayName = "Untyped SendAsync discovers response type and dispatches to handler")]
    public async Task Untyped_send_discovers_the_response_type_from_the_request()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        object boxed = new Ping("untyped");

        (await mediator.SendAsync(boxed)).ShouldBe("pong:untyped");
    }

    [Fact(DisplayName = "Untyped SendAsync throws ArgumentException when object does not implement IKyrolusRequest")]
    public async Task Untyped_send_rejects_an_object_that_is_not_a_request()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<ArgumentException>(() => mediator.SendAsync(new object()));
    }
}

public sealed record Unhandled : IKyrolusQuery<string>;
