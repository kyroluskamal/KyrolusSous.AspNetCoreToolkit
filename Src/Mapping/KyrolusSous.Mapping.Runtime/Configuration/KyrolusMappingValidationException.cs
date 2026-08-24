namespace KyrolusSous.Mapping.Runtime.Configuration;

/// <summary>
/// Exception thrown by <see cref="KyrolusMappingConfiguration.AssertConfigurationIsValid"/> when unmapped or invalid configuration rules are detected.
/// </summary>
public sealed class KyrolusMappingValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusMappingValidationException"/> class.
    /// </summary>
    /// <param name="message">The detailed validation failure message.</param>
    public KyrolusMappingValidationException(string message)
        : base(message)
    {
    }
}
