namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Defines a provider capable of resolving named validation profiles dynamically.
/// </summary>
/// <example>
/// <code>
/// public class CustomValidationProfileProvider : IKyrolusValidationProfileProvider
/// {
///     public bool TryGetProfile(string name, out KyrolusValidationContext context)
///     {
///         if (string.Equals(name, "MobileApp", StringComparison.OrdinalIgnoreCase))
///         {
///             context = new KyrolusValidationContext(Groups: ["Mobile"]);
///             return true;
///         }
///         context = KyrolusValidationContext.Default;
///         return false;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationProfileProvider
{
    /// <summary>
    /// Attempts to find a registered validation profile by its unique name.
    /// </summary>
    /// <param name="name">The name of the profile to resolve.</param>
    /// <param name="context">The resolved <see cref="KyrolusValidationContext"/> if found.</param>
    /// <returns><c>true</c> if the profile exists; otherwise, <c>false</c>.</returns>
    bool TryGetProfile(string name, out KyrolusValidationContext context);
}
