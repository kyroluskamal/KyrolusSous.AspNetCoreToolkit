namespace KyrolusSous.Resilience;

/// <summary>
/// Marker interface for mediator requests that specify a resilience pipeline name.
/// </summary>
public interface IKyrolusResilientRequest
{
    /// <summary>
    /// The name of the resilience pipeline to execute the request with.
    /// </summary>
    string PipelineName { get; }
}
