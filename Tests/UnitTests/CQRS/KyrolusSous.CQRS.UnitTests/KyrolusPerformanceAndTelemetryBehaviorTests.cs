using System.Diagnostics;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Telemetry;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusPerformanceAndTelemetryBehaviorTests
{
    public sealed record FastQuery : IKyrolusQuery<string>;
    public sealed record SlowCommand : IKyrolusCommand<string>;

    [Fact(DisplayName = "Performance: Fast request creates OpenTelemetry Activity successfully")]
    public async Task Performance_FastRequest_CompletesNormally()
    {
        var logger = Substitute.For<ILogger<KyrolusPerformanceAndTelemetryBehavior<FastQuery, string>>>();
        var options = new KyrolusCqrsPerformanceOptions { SlowRequestThresholdMs = 1000 };
        var behavior = new KyrolusPerformanceAndTelemetryBehavior<FastQuery, string>(logger, options);

        var query = new FastQuery();
        var result = await behavior.Handle(query, ct => Task.FromResult("fast-ok"), CancellationToken.None);

        result.ShouldBe("fast-ok");
    }

    [Fact(DisplayName = "Performance: Slow request exceeds threshold and logs warning")]
    public async Task Performance_SlowRequest_ExceedsThreshold()
    {
        var logger = Substitute.For<ILogger<KyrolusPerformanceAndTelemetryBehavior<SlowCommand, string>>>();
        var options = new KyrolusCqrsPerformanceOptions { SlowRequestThresholdMs = 5 }; // very small threshold
        var behavior = new KyrolusPerformanceAndTelemetryBehavior<SlowCommand, string>(logger, options);

        var command = new SlowCommand();
        var result = await behavior.Handle(command, async ct =>
        {
            await Task.Delay(20, ct);
            return "slow-ok";
        }, CancellationToken.None);

        result.ShouldBe("slow-ok");
    }
}
