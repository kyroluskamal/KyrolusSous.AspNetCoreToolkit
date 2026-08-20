namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationTracingTests
{
    #region KyrolusNoopValidationTracer
    [Fact(DisplayName = "KyrolusNoopValidationTracer Start returns null and StopAsync completes task")]
    public async Task KyrolusNoopValidationTracer_ShouldReturnNullAndCompleteTask()
    {
        var tracer = KyrolusNoopValidationTracer.Instance;
        tracer.ShouldNotBeNull();

        var traceContext = new KyrolusValidationTraceContext(typeof(object), KyrolusValidationContext.Default);
        var state = tracer.Start(traceContext);
        state.ShouldBeNull();

        await Should.NotThrowAsync(async () => await tracer.StopAsync(traceContext, state, []));
    }
    #endregion

    #region KyrolusValidationActivityTracer
    [Fact(DisplayName = "KyrolusValidationActivityTracer Start returns null when no ActivityListener is listening")]
    public void KyrolusValidationActivityTracer_StartReturnsNull_WhenNoListener()
    {
        var tracer = new KyrolusValidationActivityTracer("Kyrolus.Validation.Unlistened");
        var traceContext = new KyrolusValidationTraceContext(typeof(object), KyrolusValidationContext.Default);

        var state = tracer.Start(traceContext);
        state.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusValidationActivityTracer Start and StopAsync trace activity with tags, failures, and max severity")]
    public async Task KyrolusValidationActivityTracer_TracesActivityWithTagsAndFailures()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Kyrolus.Validation",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var tracer = new KyrolusValidationActivityTracer();
        var context = new KyrolusValidationContext(
            RuleSets: ["AdminRules"],
            Groups: ["Group1"],
            MinimumSeverity: KyrolusValidationSeverity.Warning);
        var traceContext = new KyrolusValidationTraceContext(typeof(CancellationTokenTestRequest), context);

        var state = tracer.Start(traceContext);
        state.ShouldNotBeNull();
        state.ShouldBeOfType<Activity>();

        IReadOnlyList<KyrolusValidationFailure> failures =
        [
            new KyrolusValidationFailure("Prop1", "Err1", Severity: KyrolusValidationSeverity.Info),
            new KyrolusValidationFailure("Prop2", "Err2", Severity: KyrolusValidationSeverity.Error)
        ];

        await Should.NotThrowAsync(async () => await tracer.StopAsync(traceContext, state, failures));
    }

    [Fact(DisplayName = "KyrolusValidationActivityTracer StopAsync sets Activity error status when exception is provided")]
    public async Task KyrolusValidationActivityTracer_StopAsyncSetsErrorStatus_WhenExceptionProvided()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Kyrolus.Validation.CustomSource",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var tracer = new KyrolusValidationActivityTracer("Kyrolus.Validation.CustomSource");
        var traceContext = new KyrolusValidationTraceContext(typeof(CancellationTokenTestRequest), KyrolusValidationContext.Default);

        var state = tracer.Start(traceContext);
        state.ShouldNotBeNull();
        var activity = (Activity)state;

        var exception = new InvalidOperationException("Tracing error test");
        await tracer.StopAsync(traceContext, state, [], exception);

        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("Tracing error test");
    }

    [Fact(DisplayName = "KyrolusValidationActivityTracer StopAsync handles non-Activity state gracefully")]
    public async Task KyrolusValidationActivityTracer_StopAsyncHandlesNonActivityStateGracefully()
    {
        var tracer = new KyrolusValidationActivityTracer();
        var traceContext = new KyrolusValidationTraceContext(typeof(object), KyrolusValidationContext.Default);

        await Should.NotThrowAsync(async () => await tracer.StopAsync(traceContext, new object(), []));
        await Should.NotThrowAsync(async () => await tracer.StopAsync(traceContext, null, []));
    }
    #endregion

    #region KyrolusValidationTracingHook
    [Fact(DisplayName = "KyrolusValidationTracingHook should throw ArgumentNullException when tracer is null")]
    public void KyrolusValidationTracingHook_ShouldThrowArgumentNullException_WhenTracerIsNull()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KyrolusValidationTracingHook(null!));
        exception.ParamName.ShouldBe("tracer");
    }

    [Fact(DisplayName = "KyrolusValidationTracingHook executes tracer Start and StopAsync during validation lifecycle")]
    public async Task KyrolusValidationTracingHook_ExecutesTracerLifecycle()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Kyrolus.Validation",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var tracer = new KyrolusValidationActivityTracer();
        var hook = new KyrolusValidationTracingHook(tracer);

        var request = new CancellationTokenTestRequest { Data = "Test" };
        var context = KyrolusValidationContext.Default;

        await Should.NotThrowAsync(async () =>
        {
            await hook.OnBeforeAsync(request, context);
            await hook.OnAfterAsync(request, context, []);
        });
    }
    #endregion
}
