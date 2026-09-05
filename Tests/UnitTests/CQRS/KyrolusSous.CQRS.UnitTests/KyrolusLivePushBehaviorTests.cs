using KyrolusSous.CQRS.Abstractions.Audit;
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

    public sealed record LoginNotificationCommand(string Username, string Password)
        : IKyrolusCommand<string>, IKyrolusLivePushCommand
    {
        public string Channel => "auth";
        public object? PushData => new { Username, Password };
    }

    public sealed record InternalCodeLivePushCommand(string OrderId, string InternalCode)
        : IKyrolusCommand<string>, IKyrolusLivePushCommand
    {
        public string Channel => "orders";
        public object? PushData => new { OrderId, InternalCode };
    }

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

    [Fact(DisplayName = "Live push: a field named like a built-in sensitive keyword is redacted before broadcasting")]
    public async Task Live_push_redacts_builtin_sensitive_field_before_broadcasting()
    {
        var publisher = new InMemoryLivePushPublisher();
        var behavior = new KyrolusLivePushBehavior<LoginNotificationCommand, string>(publisher);

        await behavior.Handle(
            new LoginNotificationCommand("alice", "hunter2"),
            ct => Task.FromResult("ok"),
            CancellationToken.None);

        publisher.Messages.Count.ShouldBe(1);
        var (channel, data) = publisher.Messages.Single();
        channel.ShouldBe("auth");
        var dict = data.ShouldBeOfType<Dictionary<string, object?>>();
        dict["Password"].ShouldBe("***REDACTED***"); // must never reach a live subscriber
        dict["Username"].ShouldBe("alice");
    }

    [Fact(DisplayName = "Live push: a payload with no sensitive fields broadcasts every field unchanged")]
    public async Task Live_push_no_sensitive_fields_broadcasts_unchanged()
    {
        var publisher = new InMemoryLivePushPublisher();
        var behavior = new KyrolusLivePushBehavior<OrderStatusUpdatedCommand, string>(publisher);

        await behavior.Handle(
            new OrderStatusUpdatedCommand("ord-555", "Shipped"),
            ct => Task.FromResult("Updated"),
            CancellationToken.None);

        var (_, data) = publisher.Messages.Single();
        var dict = data.ShouldBeOfType<Dictionary<string, object?>>();
        dict["OrderId"].ShouldBe("ord-555");
        dict["Status"].ShouldBe("Shipped");
    }

    [Fact(DisplayName = "Live push: an application-specific keyword supplied via KyrolusAuditSanitizationOptions is also redacted (shared with audit)")]
    public async Task Live_push_extra_keyword_from_shared_audit_options_is_redacted()
    {
        var publisher = new InMemoryLivePushPublisher();
        var options = new KyrolusAuditSanitizationOptions { AdditionalSensitiveKeywords = ["InternalCode"] };
        var behavior = new KyrolusLivePushBehavior<InternalCodeLivePushCommand, string>(publisher, sanitizationOptions: options);

        await behavior.Handle(
            new InternalCodeLivePushCommand("ord-1", "secret-code"),
            ct => Task.FromResult("ok"),
            CancellationToken.None);

        var (_, data) = publisher.Messages.Single();
        var dict = data.ShouldBeOfType<Dictionary<string, object?>>();
        dict["InternalCode"].ShouldBe("***REDACTED***");
        dict["OrderId"].ShouldBe("ord-1");
    }
}
