using System.Diagnostics.Metrics;

namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorMetricsBehaviorTests
{
    private sealed class MeasurementCollector : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<(string Instrument, long Value, KeyValuePair<string, object?>[] Tags)> LongMeasurements { get; } = [];
        public List<(string Instrument, double Value, KeyValuePair<string, object?>[] Tags)> DoubleMeasurements { get; } = [];

        public MeasurementCollector()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Kyrolus.Mediator")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                LongMeasurements.Add((instrument.Name, value, tags.ToArray())));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                DoubleMeasurements.Add((instrument.Name, value, tags.ToArray())));
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    [Fact(DisplayName = "KyrolusMediatorMetricsBehavior records a succeeded request's count and duration")]
    public async Task RecordsSucceededRequest()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder, configuration =>
            configuration.AddKyrolusMediatorMetrics()).BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        using var collector = new MeasurementCollector();
        await mediator.SendAsync(new Ping("hi"));

        var count = collector.LongMeasurements.Single(m => m.Instrument == "kyrolus.mediator.requests");
        count.Value.ShouldBe(1);
        count.Tags.ShouldContain(new KeyValuePair<string, object?>("mediator.request_type", nameof(Ping)));
        count.Tags.ShouldContain(new KeyValuePair<string, object?>("mediator.outcome", "succeeded"));

        var duration = collector.DoubleMeasurements.Single(m => m.Instrument == "kyrolus.mediator.duration");
        duration.Value.ShouldBeGreaterThanOrEqualTo(0);
        duration.Tags.ShouldContain(new KeyValuePair<string, object?>("mediator.outcome", "succeeded"));
    }

    [Fact(DisplayName = "KyrolusMediatorMetricsBehavior tags a request that throws as \"faulted\" and still rethrows it")]
    public async Task RecordsFaultedRequest_AndRethrows()
    {
        var recorder = new Recorder();
        var services = TestHost.Standard(recorder, configuration => configuration.AddKyrolusMediatorMetrics());
        services.AddTransient<IKyrolusQueryHandler<Explode, string>, ExplodeHandler>();
        services.AddTransient<IKyrolusRequestHandler<Explode, string>, ExplodeHandler>();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        using var collector = new MeasurementCollector();
        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("boom")));

        var count = collector.LongMeasurements.Single(m => m.Instrument == "kyrolus.mediator.requests");
        count.Tags.ShouldContain(new KeyValuePair<string, object?>("mediator.request_type", nameof(Explode)));
        count.Tags.ShouldContain(new KeyValuePair<string, object?>("mediator.outcome", "faulted"));
    }

    [Fact(DisplayName = "AddKyrolusMediatorMetrics is equivalent to AddOpenBehavior(typeof(KyrolusMediatorMetricsBehavior<,>))")]
    public void AddKyrolusMediatorMetrics_RegistersTheOpenBehavior()
    {
        var configuration = new KyrolusMediatorConfiguration();

        configuration.AddKyrolusMediatorMetrics();

        configuration.OpenBehaviors.ShouldContain(b =>
            b.Service == typeof(IKyrolusPipelineBehavior<,>) && b.Implementation == typeof(KyrolusMediatorMetricsBehavior<,>));
    }
}
