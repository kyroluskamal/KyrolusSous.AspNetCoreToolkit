using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusThrottlingBehaviorTests
{
    public sealed record HeavyReportQuery(string Key) : IThrottledRequest, IKyrolusQuery<string>
    {
        public string? ThrottleKey => $"tenant-report-{Key}";
        public int MaxConcurrentExecutions => 1;
        public TimeSpan ThrottleTimeout => TimeSpan.FromMilliseconds(50);
    }

    [Fact(DisplayName = "Throttling: Single request completes within limit")]
    public async Task Throttling_SingleRequest_Completes()
    {
        var behavior = new KyrolusThrottlingBehavior<HeavyReportQuery, string>();
        var query = new HeavyReportQuery("1");

        var result = await behavior.Handle(query, ct => Task.FromResult("report-data"), CancellationToken.None);
        result.ShouldBe("report-data");
    }

    [Fact(DisplayName = "Throttling: Exceeding concurrent limit throws TimeoutException")]
    public async Task Throttling_ConcurrentOverLimit_ThrowsTimeoutException()
    {
        var behavior = new KyrolusThrottlingBehavior<HeavyReportQuery, string>();
        var query = new HeavyReportQuery("blocked");

        var tcs = new TaskCompletionSource<string>();

        // Launch first task holding the 1 allowed slot
        var task1 = Task.Run(async () =>
        {
            return await behavior.Handle(query, async ct =>
            {
                return await tcs.Task;
            }, CancellationToken.None);
        });

        await Task.Delay(10); // Let task1 acquire slot

        // Second task should timeout because maxConcurrency is 1 and timeout is 50ms
        await Should.ThrowAsync<TimeoutException>(async () =>
        {
            await behavior.Handle(query, ct => Task.FromResult("never"), CancellationToken.None);
        });

        // Release first task
        tcs.SetResult("done");
        var result1 = await task1;
        result1.ShouldBe("done");
    }
}
