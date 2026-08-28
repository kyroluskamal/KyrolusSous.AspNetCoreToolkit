namespace KyrolusSous.Resilience;

/// <summary>
/// Specifies that a handler, method, or service should be wrapped with a named resilience pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ResilientAttribute : Attribute
{
    /// <summary>
    /// The name of the resilience pipeline to resolve and execute.
    /// </summary>
    public string PipelineName { get; init; } = "default";

    public ResilientAttribute() { }

    public ResilientAttribute(string pipelineName)
    {
        PipelineName = pipelineName;
    }
}

/// <summary>
/// Alias for <see cref="ResilientAttribute"/> following the Kyrolus naming pattern.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class KyrolusResilientAttribute : Attribute
{
    public string PipelineName { get; init; } = "default";

    public KyrolusResilientAttribute() { }

    public KyrolusResilientAttribute(string pipelineName)
    {
        PipelineName = pipelineName;
    }
}
