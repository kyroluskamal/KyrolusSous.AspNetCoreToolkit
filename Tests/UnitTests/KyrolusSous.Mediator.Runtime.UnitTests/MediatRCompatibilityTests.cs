namespace KyrolusSous.Mediator.Runtime.UnitTests;

/// <summary>
/// Proves that code written the MediatR way compiles and runs here. Everything in this file is
/// deliberately written using MediatR's type names and method names only.
/// </summary>
public sealed class MediatRCompatibilityTests
{
    [Fact(DisplayName = "MediatR Send alias works for request with response")]
    public async Task Send_alias_works_for_a_request_with_a_response()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new Ping("hi"));

        response.ShouldBe("pong:hi");
    }

    [Fact(DisplayName = "MediatR Send alias works for request without response")]
    public async Task Send_alias_works_for_a_request_without_a_response()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var id = Guid.NewGuid();

        await mediator.Send(new DeleteThing(id));

        recorder.Entries.ShouldContain($"deleted:{id}");
    }

    [Fact(DisplayName = "MediatR Send alias works for boxed request object")]
    public async Task Send_alias_works_for_a_boxed_request()
    {
        await using var provider = TestHost.Standard(new Recorder()).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        object boxed = new Ping("boxed");

        (await mediator.Send(boxed)).ShouldBe("pong:boxed");
    }

    [Fact(DisplayName = "MediatR Publish alias dispatches notification to handlers")]
    public async Task Publish_alias_reaches_the_handlers()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish(new SomethingHappened("x"));

        recorder.Entries.ShouldContain("first:x");
        recorder.Entries.ShouldContain("second:x");
    }

    [Fact(DisplayName = "MediatR CreateStream alias yields every streamed item")]
    public async Task CreateStream_alias_yields_every_item()
    {
        await using var provider = TestHost.Standard(new Recorder()).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new CountTo(3)))
        {
            items.Add(item);
        }

        items.ShouldBe([1, 2, 3]);
    }

    [Fact(DisplayName = "Request declared MediatR style with IRequest is handled via Send")]
    public async Task A_request_declared_the_MediatR_way_is_handled()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<IKyrolusRequestHandler<PortedRequest, string>, PortedRequestHandler>();

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        (await mediator.Send(new PortedRequest("ported"))).ShouldBe("handled:ported");
    }

    [Fact(DisplayName = "Notification declared MediatR style with INotification is handled via Publish")]
    public async Task A_notification_declared_the_MediatR_way_is_handled()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<INotificationHandler<PortedNotification>, PortedNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish(new PortedNotification("news"));

        recorder.Entries.ShouldContain("ported-notification:news");
    }

    [Fact(DisplayName = "Compatibility MediatR interfaces resolve to KyrolusMediator instance")]
    public void Compatibility_interfaces_all_resolve_to_the_mediator()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        using var provider = services.BuildServiceProvider();

        provider.GetService<IMediator>().ShouldNotBeNull();
        provider.GetService<IKyrolusMediator>().ShouldNotBeNull();
    }

    [Fact(DisplayName = "MediatR style IPipelineBehavior open behavior runs successfully")]
    public async Task MediatR_style_open_behavior_runs()
    {
        var recorder = new Recorder();
        var services = TestHost.Standard(recorder, configuration =>
            configuration.AddOpenBehavior(typeof(MediatRStyleBehavior<,>)));

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("hi"));

        recorder.Entries.ShouldContain("mediatr-style");
    }
}

// --- Types written the MediatR way ---

public sealed record PortedRequest(string Value) : IRequest<string>;

public sealed class PortedRequestHandler : IRequestHandler<PortedRequest, string>
{
    public Task<string> Handle(PortedRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"handled:{request.Value}");
}

public sealed record PortedNotification(string What) : INotification;

public sealed class PortedNotificationHandler(Recorder recorder) : INotificationHandler<PortedNotification>
{
    public Task Handle(PortedNotification notification, CancellationToken cancellationToken)
    {
        recorder.Add($"ported-notification:{notification.What}");
        return Task.CompletedTask;
    }
}

public sealed class MediatRStyleBehavior<TRequest, TResponse>(Recorder recorder)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        recorder.Add("mediatr-style");
        return next(cancellationToken);
    }
}
