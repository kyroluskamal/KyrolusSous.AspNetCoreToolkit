using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Mediator.Runtime.IntegrationTests;

public class RuntimeGeneratorIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IntegrationRecorder _recorder;
    private readonly IKyrolusMediatorSender _sender;
    private readonly IKyrolusMediatorPublisher _publisher;

    public RuntimeGeneratorIntegrationTests()
    {
        _recorder = new IntegrationRecorder();
        var services = new ServiceCollection();

        services.AddSingleton(_recorder);

        // Core Mediator registration
        services.AddKyrolusMediator();

        // Source Generator extensions generated for this assembly
        services.AddKyrolusMediatorHandlers();
        services.AddKyrolusMediatorNotificationHandlers();
        services.AddKyrolusMediatorGeneratedDispatcher();

        services.AddTransient<IKyrolusRequestExceptionAction<ExplodingRequest, InvalidOperationException>, RecordExplosionAction>();
        services.AddTransient<IKyrolusRequestExceptionHandler<ExplodingRequest, InvalidOperationException, string>, RecoverExplosionHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _sender = _serviceProvider.GetRequiredService<IKyrolusMediatorSender>();
        _publisher = _serviceProvider.GetRequiredService<IKyrolusMediatorPublisher>();
    }

    [Fact]
    public async Task SendAsync_Query_DispatchesViaGeneratedPipelineWrapperSource()
    {
        var result = await _sender.SendAsync(new GetSampleCount(21));

        result.ShouldBe(42);
        _recorder.Entries.ShouldContain("Query:21");
    }

    [Fact]
    public async Task SendAsync_Command_DispatchesViaGeneratedPipelineWrapperSource()
    {
        await _sender.SendAsync(new ExecuteSamplePing());

        _recorder.Entries.ShouldContain("CommandHandled");
    }

    [Fact]
    public async Task StreamAsync_StreamRequest_DispatchesViaGeneratedStreamWrapper()
    {
        var numbers = new List<int>();
        await foreach (var item in _sender.StreamAsync(new StreamSampleNumbers(3)))
        {
            numbers.Add(item);
        }

        numbers.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task PublishAsync_Notification_DispatchesViaGeneratedNotificationDispatchSource()
    {
        await _publisher.PublishAsync(new SampleEvent("hello"));

        _recorder.Entries.ShouldContain("Handler1:hello");
        _recorder.Entries.ShouldContain("Handler2:hello");
    }

    [Fact]
    public async Task SendAsync_ExceptionActionAndHandler_DispatchesViaGeneratedExceptionDispatchSource()
    {
        var result = await _sender.SendAsync(new ExplodingRequest());

        result.ShouldBe("recovered_fallback");
        _recorder.Entries.ShouldContain("ActionRecorded:Sample explosion");
        _recorder.Entries.ShouldContain("HandlerRecovered");
    }
}
