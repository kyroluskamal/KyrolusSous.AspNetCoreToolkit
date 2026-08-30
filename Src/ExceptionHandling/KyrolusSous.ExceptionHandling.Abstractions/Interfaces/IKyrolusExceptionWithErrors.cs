namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

/// <summary>
/// Implemented by exceptions that carry a collection of structured <see cref="KyrolusErrorItem"/> field errors.
/// </summary>
public interface IKyrolusExceptionWithErrors
{
    /// <summary>
    /// Gets the list of field-level errors associated with this exception.
    /// </summary>
    /// <returns>A read-only list of error items, or <c>null</c>.</returns>
    IReadOnlyList<KyrolusErrorItem>? GetErrors();
}
