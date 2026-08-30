namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents a developer configuration or startup violation in <see cref="Models.KyrolusErrorCodeRegistry"/>.
/// </summary>
/// <remarks>
/// This exception inherits directly from <see cref="Exception"/> (not <see cref="KyrolusException"/>)
/// because it represents an internal system setup bug or strict-mode governance violation that must fail-fast during bootstrapping.
/// </remarks>
/// <param name="message">The description of the registry violation.</param>
public sealed class KyrolusErrorCodeRegistryException(string message) : Exception(message)
{
}
