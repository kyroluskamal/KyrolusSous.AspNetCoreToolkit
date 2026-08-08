namespace KyrolusSous.Mediator.Runtime.UnitTests;

public class PipelineWrappersTests
{
    [Fact(DisplayName = "PipelineWrapperFactory creates request pipeline wrapper for request and response type")]
    public void PipelineWrapperFactory_CreatesRequestWrapper()
    {
        var requestWrapper = GeneratorIntegration.KyrolusPipelineWrapperFactory.CreateRequest<Ping, string>();
        requestWrapper.ShouldNotBeNull();
    }
}
