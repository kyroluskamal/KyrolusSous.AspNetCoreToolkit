namespace KyrolusSous.SourceMediator.Attributes;

/// <summary>
/// Specifies the execution order for a pipeline behavior implementation.
/// Behaviors are sorted based on the specified order before execution.
/// Lower order values typically execute earlier (forming the outer layers of the pipeline).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PipelineOrderAttribute"/> class 
/// with the specified execution order.
/// </remarks>
/// <param name="order">The execution order value. Lower values indicate earlier execution.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PipelineOrderAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets the execution order value. Lower values indicate earlier execution (outer layers).
    /// </summary>
    public int Order { get; } = order;
}