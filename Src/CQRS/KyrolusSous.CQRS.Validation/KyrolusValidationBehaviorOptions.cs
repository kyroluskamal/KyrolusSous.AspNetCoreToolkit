namespace KyrolusSous.CQRS.Validation;

/// <summary>
/// Configures <see cref="KyrolusValidationBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class KyrolusValidationBehaviorOptions
{
    /// <summary>
    /// The minimum <see cref="KyrolusValidationSeverity"/> a collected failure must reach to block the
    /// request. Failures below this level are dropped instead of throwing - matching the documented
    /// meaning of <see cref="KyrolusValidationSeverity.Info"/> and <see cref="KyrolusValidationSeverity.Warning"/>
    /// as non-blocking hints rather than rejections.
    /// </summary>
    public KyrolusValidationSeverity MinimumBlockingSeverity { get; set; } = KyrolusValidationSeverity.Error;
}
