namespace KyrolusSous.Mediator.Runtime.UnitTests;

public class PipelineWrapperFactoryTests
{
    [Fact]
    public void PipelineWrapperFactory_CreatesRequestWrapper()
    {
        var requestWrapper = KyrolusPipelineWrapperFactory.CreateRequest<Ping, string>();
        requestWrapper.ShouldNotBeNull();
    }
}
