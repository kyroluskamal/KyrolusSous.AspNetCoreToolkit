using Shouldly;
using Xunit;

namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorCancellationTokenTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    [Fact(DisplayName = "SendAsync for Query with pre-cancelled token throws OperationCanceledException")]
    public async Task SendAsync_query_with_cancelled_token_throws()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await mediator.SendAsync(new Ping("test"), cts.Token);
        });
    }

    [Fact(DisplayName = "SendAsync for Command with response with pre-cancelled token throws OperationCanceledException")]
    public async Task SendAsync_command_with_response_with_cancelled_token_throws()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await mediator.SendAsync(new CreateThing("widget"), cts.Token);
        });
    }

    [Fact(DisplayName = "SendAsync for void Command with pre-cancelled token throws OperationCanceledException")]
    public async Task SendAsync_void_command_with_cancelled_token_throws()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await mediator.SendAsync(new DeleteThing(Guid.NewGuid()), cts.Token);
        });
    }

    [Fact(DisplayName = "Untyped SendAsync with pre-cancelled token throws OperationCanceledException")]
    public async Task Untyped_send_with_cancelled_token_throws()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        object untypedRequest = new Ping("untyped");

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await mediator.SendAsync(untypedRequest, cts.Token);
        });
    }

    [Fact(DisplayName = "PublishAsync with pre-cancelled token throws OperationCanceledException")]
    public async Task PublishAsync_with_cancelled_token_throws()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await mediator.PublishAsync(new SomethingHappened("event"), cts.Token);
        });
    }

    [Fact(DisplayName = "Untyped PublishAsync with pre-cancelled token throws OperationCanceledException")]
    public async Task Untyped_publish_with_cancelled_token_throws()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        object untypedNotification = new SomethingHappened("event");

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await mediator.PublishAsync(untypedNotification, cts.Token);
        });
    }

    [Fact(DisplayName = "StreamAsync with pre-cancelled token throws OperationCanceledException")]
    public async Task StreamAsync_with_cancelled_token_throws()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.StreamAsync(new CountTo(5), cts.Token))
            {
            }
        });
    }

    [Fact(DisplayName = "CancellationToken is passed unchanged through pipeline to request handler")]
    public async Task CancellationToken_is_passed_to_handler()
    {
        CancellationToken capturedToken = default;
        var services = TestHost.Standard(new Recorder());
        services.AddTransient<IKyrolusRequestHandler<TokenTestRequest, string>>(_ => new TokenCapturingHandler(token => capturedToken = token));

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        using var cts = new CancellationTokenSource();
        var response = await mediator.SendAsync(new TokenTestRequest("hello"), cts.Token);

        response.ShouldBe("pong:hello");
        capturedToken.ShouldBe(cts.Token);
    }

    private sealed record TokenTestRequest(string Message) : IKyrolusRequest<string>;

    private sealed class TokenCapturingHandler(Action<CancellationToken> onHandle) : IKyrolusRequestHandler<TokenTestRequest, string>
    {
        public Task<string> Handle(TokenTestRequest request, CancellationToken cancellationToken)
        {
            onHandle(cancellationToken);
            return Task.FromResult($"pong:{request.Message}");
        }
    }
}
