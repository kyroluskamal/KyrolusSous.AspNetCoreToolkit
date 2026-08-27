namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// The concrete <see cref="IKyrolusExternalAuthProvider"/> the provider packages register.
/// One shared type keeps five near-identical private classes from existing.
/// </summary>
/// <param name="providerName">The canonical provider name.</param>
/// <param name="authenticationScheme">The scheme the provider was registered under.</param>
/// <param name="displayName">The name to show on a sign-in button.</param>
/// <param name="isConfigured">Whether the provider has the credentials it needs.</param>
public sealed class KyrolusExternalAuthProviderDescriptor(
    string providerName,
    string authenticationScheme,
    string displayName,
    bool isConfigured) : IKyrolusExternalAuthProvider
{
    /// <inheritdoc />
    public string ProviderName { get; } = providerName;

    /// <inheritdoc />
    public string AuthenticationScheme { get; } = authenticationScheme;

    /// <inheritdoc />
    public string DisplayName { get; } = displayName;

    /// <inheritdoc />
    public bool IsConfigured { get; } = isConfigured;
}
