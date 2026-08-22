namespace KyrolusSous.Resilience;

public interface IKyrolusCustomPipelineConfigurator
{
    string Name { get; }

    void Configure(ResiliencePipelineBuilder builder);
}

public class DelegateCustomPipelineConfigurator(string name, Action<ResiliencePipelineBuilder> configure) : IKyrolusCustomPipelineConfigurator
{
    public string Name { get; } = name;

    public void Configure(ResiliencePipelineBuilder builder) => configure(builder);
}
