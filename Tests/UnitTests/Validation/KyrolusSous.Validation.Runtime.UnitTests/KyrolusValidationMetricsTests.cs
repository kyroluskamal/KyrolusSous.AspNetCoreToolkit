using System.Diagnostics.Metrics;

namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationMetricsTests
{
    #region KyrolusValidationMetricsHook
    [Fact(DisplayName = "KyrolusValidationMetricsHook should throw ArgumentNullException when metrics is null")]
    public void KyrolusValidationMetricsHook_ShouldThrowArgumentNullException_WhenMetricsIsNull()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KyrolusValidationMetricsHook(null!));
        exception.ParamName.ShouldBe("metrics");
    }

    [Fact(DisplayName = "KyrolusValidationMetricsHook should measure elapsed time and record metrics on full lifecycle")]
    public async Task KyrolusValidationMetricsHook_ShouldMeasureElapsedTimeAndRecordMetrics()
    {
        KyrolusValidationMetricsContext? recordedContext = null;
        var metrics = new KyrolusDelegateValidationMetrics((ctx, token) =>
        {
            recordedContext = ctx;
            return ValueTask.CompletedTask;
        });

        var hook = new KyrolusValidationMetricsHook(metrics);
        var request = new CancellationTokenTestRequest { Data = "Test" };
        var context = KyrolusValidationContext.Default;
        IReadOnlyList<KyrolusValidationFailure> failures = [new KyrolusValidationFailure("Prop", "Error")];

        await hook.OnBeforeAsync(request, context);
        await Task.Delay(20);
        await hook.OnAfterAsync(request, context, failures);

        recordedContext.ShouldNotBeNull();
        recordedContext.RequestType.ShouldBe(typeof(CancellationTokenTestRequest));
        recordedContext.Context.ShouldBe(context);
        recordedContext.Failures.ShouldBe(failures);
        recordedContext.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact(DisplayName = "KyrolusValidationMetricsHook should record TimeSpan.Zero when OnAfterAsync is called without OnBeforeAsync")]
    public async Task KyrolusValidationMetricsHook_ShouldRecordTimeSpanZero_WhenOnBeforeAsyncNotCalled()
    {
        KyrolusValidationMetricsContext? recordedContext = null;
        var metrics = new KyrolusDelegateValidationMetrics((ctx, token) =>
        {
            recordedContext = ctx;
            return ValueTask.CompletedTask;
        });

        var hook = new KyrolusValidationMetricsHook(metrics);
        var request = new CancellationTokenTestRequest { Data = "Test" };

        await hook.OnAfterAsync(request, KyrolusValidationContext.Default, []);

        recordedContext.ShouldNotBeNull();
        recordedContext.Duration.ShouldBe(TimeSpan.Zero);
    }

    [Fact(DisplayName = "KyrolusValidationMetricsHook should handle null request gracefully")]
    public async Task KyrolusValidationMetricsHook_ShouldHandleNullRequestGracefully()
    {
        KyrolusValidationMetricsContext? recordedContext = null;
        var metrics = new KyrolusDelegateValidationMetrics((ctx, token) =>
        {
            recordedContext = ctx;
            return ValueTask.CompletedTask;
        });

        var hook = new KyrolusValidationMetricsHook(metrics);

        await hook.OnBeforeAsync(null, KyrolusValidationContext.Default);
        await hook.OnAfterAsync(null, KyrolusValidationContext.Default, []);

        recordedContext.ShouldNotBeNull();
        recordedContext.RequestType.ShouldBeNull();
    }
    #endregion

    #region KyrolusNoopValidationMetrics
    [Fact(DisplayName = "KyrolusNoopValidationMetrics should complete task successfully")]
    public async Task KyrolusNoopValidationMetrics_ShouldCompleteSuccessfully()
    {
        var metrics = KyrolusNoopValidationMetrics.Instance;
        metrics.ShouldNotBeNull();

        var metricsContext = new KyrolusValidationMetricsContext(typeof(object), KyrolusValidationContext.Default, [], TimeSpan.FromSeconds(1));
        await metrics.RecordAsync(metricsContext);
    }
    #endregion

    #region KyrolusDelegateValidationMetrics
    [Fact(DisplayName = "KyrolusDelegateValidationMetrics should throw ArgumentNullException when execute is null")]
    public void KyrolusDelegateValidationMetrics_ShouldThrowArgumentNullException_WhenExecuteIsNull()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KyrolusDelegateValidationMetrics(null!));
        exception.ParamName.ShouldBe("execute");
    }

    [Fact(DisplayName = "KyrolusDelegateValidationMetrics should invoke delegate on RecordAsync")]
    public async Task KyrolusDelegateValidationMetrics_ShouldInvokeDelegateOnRecordAsync()
    {
        var executed = false;
        var metrics = new KyrolusDelegateValidationMetrics((ctx, token) =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        });

        var metricsContext = new KyrolusValidationMetricsContext(typeof(object), KyrolusValidationContext.Default, [], TimeSpan.FromMilliseconds(50));
        await metrics.RecordAsync(metricsContext);

        executed.ShouldBeTrue();
    }
    #endregion

    #region KyrolusValidationSystemMetrics
    [Fact(DisplayName = "KyrolusValidationSystemMetrics should throw ArgumentException when meterName is null, empty, or whitespace")]
    public void KyrolusValidationSystemMetrics_ShouldThrowArgumentException_WhenMeterNameIsNullOrWhitespace()
    {
        Should.Throw<ArgumentException>(() => new KyrolusValidationSystemMetrics(null!));
        Should.Throw<ArgumentException>(() => new KyrolusValidationSystemMetrics(""));
        Should.Throw<ArgumentException>(() => new KyrolusValidationSystemMetrics("   "));
    }

    [Fact(DisplayName = "KyrolusValidationSystemMetrics should construct successfully with default arguments")]
    public void KyrolusValidationSystemMetrics_ShouldConstructSuccessfully_WithDefaultArguments()
    {
        using var metrics = new KyrolusValidationSystemMetrics();
        metrics.ShouldNotBeNull();
    }

    [Fact(DisplayName = "KyrolusValidationSystemMetrics should record executions and duration, but not failures, for a passing result")]
    public async Task KyrolusValidationSystemMetrics_ShouldRecordExecutionAndDuration_ForPassingResult()
    {
        var meterName = $"Test.Kyrolus.Validation.{Guid.NewGuid()}";
        var metrics = new KyrolusValidationSystemMetrics(meterName);
        using var collector = new MeasurementCollector(meterName);

        var context = new KyrolusValidationMetricsContext(
            typeof(CancellationTokenTestRequest), KyrolusValidationContext.Default, [], TimeSpan.FromMilliseconds(42));
        await metrics.RecordAsync(context);

        var execution = collector.LongMeasurements.ShouldHaveSingleItem();
        execution.Instrument.ShouldBe("kyrolus.validation.executions");
        execution.Value.ShouldBe(1);
        execution.Tags.ShouldContain(new KeyValuePair<string, object?>("validation.request_type", nameof(CancellationTokenTestRequest)));
        execution.Tags.ShouldContain(new KeyValuePair<string, object?>("validation.outcome", "passed"));

        var elapsed = collector.DoubleMeasurements.ShouldHaveSingleItem();
        elapsed.Instrument.ShouldBe("kyrolus.validation.duration");
        elapsed.Value.ShouldBe(42);
    }

    [Fact(DisplayName = "KyrolusValidationSystemMetrics should record executions, duration, and failures tagged with max severity, for a failing result")]
    public async Task KyrolusValidationSystemMetrics_ShouldRecordFailures_ForFailingResult()
    {
        var meterName = $"Test.Kyrolus.Validation.{Guid.NewGuid()}";
        var metrics = new KyrolusValidationSystemMetrics(meterName);
        using var collector = new MeasurementCollector(meterName);

        IReadOnlyList<KyrolusValidationFailure> failures =
        [
            new("Prop1", "Error1", Severity: KyrolusValidationSeverity.Warning),
            new("Prop2", "Error2", Severity: KyrolusValidationSeverity.Error)
        ];
        var context = new KyrolusValidationMetricsContext(
            typeof(CancellationTokenTestRequest), KyrolusValidationContext.Default, failures, TimeSpan.FromMilliseconds(10));
        await metrics.RecordAsync(context);

        var execution = collector.LongMeasurements.Single(m => m.Instrument == "kyrolus.validation.executions");
        execution.Tags.ShouldContain(new KeyValuePair<string, object?>("validation.outcome", "failed"));

        var failureMeasurement = collector.LongMeasurements.Single(m => m.Instrument == "kyrolus.validation.failures");
        failureMeasurement.Value.ShouldBe(2);
        failureMeasurement.Tags.ShouldContain(new KeyValuePair<string, object?>("validation.request_type", nameof(CancellationTokenTestRequest)));
        failureMeasurement.Tags.ShouldContain(new KeyValuePair<string, object?>("validation.max_severity", KyrolusValidationSeverity.Error.ToString()));
    }

    [Fact(DisplayName = "KyrolusValidationSystemMetrics should tag the request type as Unknown when RequestType is null")]
    public async Task KyrolusValidationSystemMetrics_ShouldTagUnknownRequestType_WhenRequestTypeIsNull()
    {
        var meterName = $"Test.Kyrolus.Validation.{Guid.NewGuid()}";
        var metrics = new KyrolusValidationSystemMetrics(meterName);
        using var collector = new MeasurementCollector(meterName);

        var context = new KyrolusValidationMetricsContext(null, KyrolusValidationContext.Default, [], TimeSpan.Zero);
        await metrics.RecordAsync(context);

        var execution = collector.LongMeasurements.ShouldHaveSingleItem();
        execution.Tags.ShouldContain(new KeyValuePair<string, object?>("validation.request_type", "Unknown"));
    }

    /// <summary>Captures measurements emitted on a single named <see cref="Meter"/>, for asserting against in tests.</summary>
    private sealed class MeasurementCollector : IDisposable
    {
        private readonly MeterListener listener = new();

        public List<(string Instrument, long Value, KeyValuePair<string, object?>[] Tags)> LongMeasurements { get; } = [];
        public List<(string Instrument, double Value, KeyValuePair<string, object?>[] Tags)> DoubleMeasurements { get; } = [];

        public MeasurementCollector(string meterName)
        {
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName)
                    l.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                LongMeasurements.Add((instrument.Name, value, tags.ToArray())));
            listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                DoubleMeasurements.Add((instrument.Name, value, tags.ToArray())));
            listener.Start();
        }

        public void Dispose() => listener.Dispose();
    }
    #endregion
}
