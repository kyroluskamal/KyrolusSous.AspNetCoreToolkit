namespace KyrolusSous.Mediator.Runtime.UnitTests;

public class PipelineWrappersTests
{
    [Fact(DisplayName = "PipelineWrapperFactory creates request pipeline wrapper for request and response type")]
    public void PipelineWrapperFactory_CreatesRequestWrapper()
    {
        var requestWrapper = KyrolusPipelineWrapperFactory.CreateRequest<Ping, string>();
        requestWrapper.ShouldNotBeNull();
        requestWrapper.ShouldBeOfType<RequestPipelineWrapperImpl<Ping, string>>();
    }

    [Fact(DisplayName = "PipelineWrapperFactory creates stream pipeline wrapper for stream request and response type")]
    public void PipelineWrapperFactory_CreatesStreamWrapper()
    {
        var streamWrapper = KyrolusPipelineWrapperFactory.CreateStream<CountTo, int>();
        streamWrapper.ShouldNotBeNull();
        streamWrapper.ShouldBeOfType<StreamPipelineWrapperImpl<CountTo, int>>();
    }
}
