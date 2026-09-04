using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.LivePush;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusLivePushBehaviorTests
{
    public sealed record OrderStatusUpdatedCommand(string OrderId, string Status)
        : IKyrolusCommand<string>, IKyrolusLivePushCommand
    {
        public string Channel => "orders";
        public object? PushData => new { OrderId, Status };
    }

    public sealed record PlainCommand(string Action) : IKyrolusCommand<string>;

    private sealed class ThrowingPushPublisher : IKyrolusLivePushPublisher
    {
        public Task PublishLiveAsync(string channel, object? data, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("WebSocket closed");
    }

    [Fact(DisplayName = "Live push command should broadcast to publisher on success")]
    public async Task Live_push_command_should_broadcast_to_publisher_on_success()
    {
        var publisher = new InMemoryLivePushPublisher();
        var behavior = new KyrolusLivePushBehavior<OrderStatusUpdatedCommand, string>(publisher);
        var cmd = new OrderStatusUpdatedCommand("ord-555", "Shipped");

        var response = await behavior.Handle(cmd, ct => Task.FromResult("Updated"), CancellationToken.None);

        response.ShouldBe("Updated");
        publisher.Messages.Count.ShouldBe(1);

        var (channel, data) = publisher.Messages.First();
        channel.ShouldBe("orders");
        data.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Live push publisher exception should be isolated and not throw")]
    public async Task Live_push_publisher_exception_should_be_isolated_and_not_throw()
    {
        var publisher = new ThrowingPushPublisher();
        var behavior = new KyrolusLivePushBehavior<OrderStatusUpdatedCommand, string>(publisher);
        var cmd = new OrderStatusUpdatedCommand("ord-555", "Shipped");

        var response = await behavior.Handle(cmd, ct => Task.FromResult("Updated"), CancellationToken.None);

        response.ShouldBe("Updated");
    }

    [Fact(DisplayName = "Plain command should not broadcast to publisher")]
    public async Task Plain_command_should_not_broadcast_to_publisher()
    {
        var publisher = new InMemoryLivePushPublisher();
        var behavior = new KyrolusLivePushBehavior<PlainCommand, string>(publisher);
        var cmd = new PlainCommand("test");

        var response = await behavior.Handle(cmd, ct => Task.FromResult("ok"), CancellationToken.None);

        response.ShouldBe("ok");
        publisher.Messages.ShouldBeEmpty();
    }
}
