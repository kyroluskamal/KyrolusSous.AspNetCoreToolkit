using Polly;

namespace KyrolusSous.Resilience;

/// <summary>
/// Contract for defining custom resilience pipeline configurations.
/// </summary>
public interface IKyrolusCustomPipelineConfigurator
{
    /// <summary>
    /// Unique pipeline name this configurator applies to.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Configures the resilience pipeline builder with custom strategies.
    /// </summary>
    void Configure(ResiliencePipelineBuilder builder);
}

/// <summary>
/// Delegate-based implementation of <see cref="IKyrolusCustomPipelineConfigurator"/>.
/// </summary>
public class KyrolusDelegateCustomPipelineConfigurator(string name, Action<ResiliencePipelineBuilder> configure)
    : IKyrolusCustomPipelineConfigurator
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public void Configure(ResiliencePipelineBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        configure(builder);
    }
}
