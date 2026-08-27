namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Hook invoked after an external provider has authenticated a user but before the local
/// authentication ticket is issued. Implement it to resolve or provision the local account
/// and to attach local claims (user id, roles, tenant) to the principal.
/// </summary>
/// <remarks>
/// This is the seam that keeps the provider packages storage-agnostic: they know how to talk
/// to Google or Apple, and nothing at all about where users live.
/// <c>KyrolusSous.Auth.Runtime</c> ships a default implementation built on
/// <see cref="IKyrolusAuthUserStore"/>.
/// </remarks>
public interface IKyrolusExternalLoginHandler
{
    /// <summary>
    /// Resolves the local identity for an external login.
    /// </summary>
    /// <param name="info">The normalised external identity.</param>
    /// <param name="options">The options the provider was registered with.</param>
    /// <param name="cancellationToken">A token that aborts the operation.</param>
    ValueTask<KyrolusExternalLoginResult> HandleAsync(
        KyrolusExternalLoginInfo info,
        KyrolusExternalLoginOptions options,
        CancellationToken cancellationToken = default);
}
