
using KyrolusSous.Mediator.Abstractions.Compatibility;

namespace KyrolusSous.Mediator.Runtime.UnitTests;

/// <summary>
/// Proves that code written the MediatR way compiles and runs here. Everything in this file is
/// deliberately written using MediatR's type names and method names only.
/// </summary>
public sealed class MediatRCompatibilityTests
{
    [Fact]
    public async Task Send_alias_works_for_a_request_with_a_response()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new Ping("hi"));

        response.ShouldBe("pong:hi");
    }

    [Fact]
    public async Task Send_alias_works_for_a_request_without_a_response()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var id = Guid.NewGuid();

        await mediator.Send(new DeleteThing(id));

        recorder.Entries.ShouldContain($"deleted:{id}");
    }

    [Fact]
    public async Task Send_alias_works_for_a_boxed_request()
    {
        await using var provider = TestHost.Standard(new Recorder()).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        object boxed = new Ping("boxed");

        (await mediator.Send(boxed)).ShouldBe("pong:boxed");
    }

    [Fact]
    public async Task Publish_alias_reaches_the_handlers()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish(new SomethingHappened("x"));

        recorder.Entries.ShouldContain("first:x");
        recorder.Entries.ShouldContain("second:x");
    }

    [Fact]
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

    /// <summary>
    /// A request and handler declared entirely in MediatR's vocabulary - <c>IRequest</c> and
    /// <c>IRequestHandler</c> - dispatched through <c>Send</c>.
    /// </summary>
    [Fact]
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

    /// <summary>A notification handler declared the MediatR way.</summary>
    [Fact]
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

    [Fact]
    public void Compatibility_interfaces_all_resolve_to_the_mediator()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        using var provider = services.BuildServiceProvider();

        provider.GetService<IMediator>().ShouldNotBeNull();
        provider.GetService<IKyrolusMediator>().ShouldNotBeNull();
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
