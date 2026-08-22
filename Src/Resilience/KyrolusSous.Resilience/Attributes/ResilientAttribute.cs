namespace KyrolusSous.Resilience;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class ResilientAttribute : Attribute
{
    public string PipelineName { get; set; } = "default";

    public ResilientAttribute() { }

    public ResilientAttribute(string pipelineName)
    {
        PipelineName = pipelineName;
    }
}

public interface IResilientRequest
{
    string PipelineName => "default";
}
