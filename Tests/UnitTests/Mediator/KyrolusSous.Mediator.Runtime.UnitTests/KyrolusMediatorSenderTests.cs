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

    [Fact(DisplayName = "Sender caches request pipeline wrapper instance across multiple calls")]
    public async Task Sender_caches_request_pipeline_wrapper_instance_across_multiple_calls()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("first"));
        await mediator.SendAsync(new Ping("second"));

        var field = typeof(KyrolusMediatorSender).GetField("s_requestWrappers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field.ShouldNotBeNull();

        var dict = (System.Collections.IDictionary)field!.GetValue(null)!;
        dict.ShouldNotBeNull();

        var wrapperKey = (typeof(Ping), typeof(string));
        dict.Contains(wrapperKey).ShouldBeTrue();

        var cachedWrapper = dict[wrapperKey];
        cachedWrapper.ShouldNotBeNull();

        // Send again to ensure same cached instance is used
        await mediator.SendAsync(new Ping("third"));
        dict[wrapperKey].ShouldBeSameAs(cachedWrapper);
    }

    [Fact(DisplayName = "Sender caches stream pipeline wrapper instance across multiple calls")]
    public async Task Sender_caches_stream_pipeline_wrapper_instance_across_multiple_calls()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var items1 = await mediator.StreamAsync(new CountTo(2)).ToListAsync();
        items1.ShouldNotBeEmpty();

        var field = typeof(KyrolusMediatorSender).GetField("s_streamWrappers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field.ShouldNotBeNull();

        var dict = (System.Collections.IDictionary)field!.GetValue(null)!;
        dict.ShouldNotBeNull();

        var wrapperKey = (typeof(CountTo), typeof(int));
        dict.Contains(wrapperKey).ShouldBeTrue();

        var cachedWrapper = dict[wrapperKey];
        cachedWrapper.ShouldNotBeNull();

        var items2 = await mediator.StreamAsync(new CountTo(2)).ToListAsync();
        items2.ShouldNotBeEmpty();
        dict[wrapperKey].ShouldBeSameAs(cachedWrapper);
    }

    [Fact(DisplayName = "KyrolusMediatorSender constructor throws ArgumentNullException when serviceProvider or dispatcher is null")]
    public async Task Constructor_throws_ArgumentNullException_when_serviceProvider_or_dispatcher_is_null()
    {
        await using var provider = Build(new Recorder());
        var dispatcher = provider.GetRequiredService<IMediatorDispatcher>();

        Should.Throw<ArgumentNullException>(() => new KyrolusMediatorSender(null!, dispatcher)).ParamName.ShouldBe("serviceProvider");
        Should.Throw<ArgumentNullException>(() => new KyrolusMediatorSender(provider, null!)).ParamName.ShouldBe("dispatcher");
    }

    [Fact(DisplayName = "Sender throws InvalidOperationException when no pipeline wrapper source is registered")]
    public async Task Sender_throws_InvalidOperationException_when_no_pipeline_wrapper_source_is_registered()
    {
        var services = new ServiceCollection();
        var dispatcher = new DummyDispatcher();
        services.AddSingleton<IMediatorDispatcher>(dispatcher);
        await using var provider = services.BuildServiceProvider();

        var sender = new KyrolusMediatorSender(provider, dispatcher);

        var ex1 = await Should.ThrowAsync<InvalidOperationException>(() => sender.SendAsync(new UnregisteredQuery()));
        ex1.Message.ShouldContain("No pipeline wrapper source is registered");
    }

    [Fact(DisplayName = "Sender throws InvalidOperationException when wrapper source returns null for request wrapper")]
    public async Task Sender_throws_InvalidOperationException_when_wrapper_source_returns_null_request_wrapper()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratorIntegration.IKyrolusPipelineWrapperSource>(new NullWrapperSource());
        var dispatcher = new DummyDispatcher();
        services.AddSingleton<IMediatorDispatcher>(dispatcher);
        await using var provider = services.BuildServiceProvider();

        var sender = new KyrolusMediatorSender(provider, dispatcher);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sender.SendAsync(new NullWrapperQuery()));
        ex.Message.ShouldContain("No pipeline wrapper for");
    }

    [Fact(DisplayName = "Sender throws InvalidOperationException when wrapper source returns null for stream wrapper")]
    public async Task Sender_throws_InvalidOperationException_when_wrapper_source_returns_null_stream_wrapper()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratorIntegration.IKyrolusPipelineWrapperSource>(new NullWrapperSource());
        var dispatcher = new DummyDispatcher();
        services.AddSingleton<IMediatorDispatcher>(dispatcher);
        await using var provider = services.BuildServiceProvider();

        var sender = new KyrolusMediatorSender(provider, dispatcher);

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in sender.StreamAsync(new NullStreamQuery()))
            {
            }
        });
        ex.Message.ShouldContain("No pipeline wrapper for");
    }

    private sealed class DummyDispatcher : IMediatorDispatcher
    {
        public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct) => throw new NotImplementedException();
        public Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class NullWrapperSource : GeneratorIntegration.IKyrolusPipelineWrapperSource
    {
        public object? CreateRequestWrapper(Type requestType, Type responseType) => null;
        public object? CreateStreamWrapper(Type requestType, Type responseType) => null;
        public Type? GetResponseType(Type requestType, bool stream) => null;
    }
}

public sealed record Unhandled : IKyrolusQuery<string>;
public sealed record UnregisteredQuery : IKyrolusQuery<string>;
public sealed record NullWrapperQuery : IKyrolusQuery<string>;
public sealed record NullStreamQuery : IKyrolusStreamRequest<int>;
