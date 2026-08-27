namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Describes a registered external authentication provider (Google, Apple, Facebook, ...).
/// Inject <see cref="IEnumerable{T}"/> of this type to render sign-in buttons without
/// hard-coding which providers an application happens to have enabled.
/// </summary>
public interface IKyrolusExternalAuthProvider
{
    /// <summary>
    /// Gets the canonical provider name (for example <c>"Google"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the ASP.NET Core authentication scheme this provider is registered under.
    /// Usually equal to <see cref="ProviderName"/>, but distinct when the same provider is
    /// registered more than once.
    /// </summary>
    string AuthenticationScheme { get; }

    /// <summary>
    /// Gets the human-readable name to show on a sign-in button.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether this provider has the credentials it needs to work.
    /// </summary>
    bool IsConfigured { get; }
}
