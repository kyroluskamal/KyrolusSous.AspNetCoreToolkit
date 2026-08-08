namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusStreamPassThroughBehaviorTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    [Fact(DisplayName = "StreamAsync yields every item produced by stream request handler")]
    public async Task Stream_request_yields_every_item()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.StreamAsync(new CountTo(4)))
        {
            items.Add(item);
        }

        items.ShouldBe([1, 2, 3, 4]);
    }

    [Fact(DisplayName = "Untyped StreamAsync yields boxed item stream from stream request handler")]
    public async Task Untyped_stream_yields_boxed_items()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        object boxed = new CountTo(3);

        var items = new List<object?>();
        await foreach (var item in mediator.StreamAsync(boxed))
        {
            items.Add(item);
        }

        items.ShouldBe([1, 2, 3]);
    }

    [Fact(DisplayName = "StreamAsync respects cancellation token and stops enumeration when cancelled")]
    public async Task Cancelling_a_stream_stops_it()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();

        var seen = 0;
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.StreamAsync(new CountTo(1000), cts.Token))
            {
                seen++;
                if (seen == 3)
                {
                    await cts.CancelAsync();
                }
            }
        });

        seen.ShouldBe(3);
    }
}
