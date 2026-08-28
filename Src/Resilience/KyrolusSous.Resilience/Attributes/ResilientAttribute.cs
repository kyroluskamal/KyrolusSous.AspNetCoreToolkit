namespace KyrolusSous.Resilience;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class KyrolusResilientAttribute : Attribute
{
    public string PipelineName { get; set; } = "default";

    public KyrolusResilientAttribute() { }

    public KyrolusResilientAttribute(string pipelineName)
    {
        PipelineName = pipelineName;
    }
}

public interface IKyrolusResilientRequest
{
    string PipelineName => "default";
}
