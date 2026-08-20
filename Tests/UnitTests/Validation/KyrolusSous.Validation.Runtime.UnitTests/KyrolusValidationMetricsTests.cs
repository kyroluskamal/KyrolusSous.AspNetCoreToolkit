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
}
