using System.Security.Claims;

namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// A provider-neutral view of the identity returned by an external login, normalised from the
/// wildly different shapes Google, Apple, GitHub and friends each hand back.
/// </summary>
public sealed class KyrolusExternalLoginInfo
{
    /// <summary>
    /// Gets the canonical provider name (for example <c>"Google"</c>).
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets the provider's stable identifier for this user (the <c>sub</c> / <c>id</c> claim).
    /// Together with <see cref="ProviderName"/> this is the natural key for a local login record.
    /// </summary>
    public required string ProviderKey { get; init; }

    /// <summary>
    /// Gets the principal the provider's handler built, including every mapped claim.
    /// </summary>
    public required ClaimsPrincipal Principal { get; init; }

    /// <summary>Gets the email address reported by the provider, if any.</summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets whether the provider states the email address has been verified.
    /// Providers that do not report verification status leave this <c>false</c>.
    /// </summary>
    public bool EmailVerified { get; init; }

    /// <summary>Gets the user's display name, if any.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the user's given (first) name, if any.</summary>
    public string? GivenName { get; init; }

    /// <summary>Gets the user's family (last) name, if any.</summary>
    public string? FamilyName { get; init; }

    /// <summary>Gets the URL of the user's avatar, if any.</summary>
    public string? PictureUrl { get; init; }

    /// <summary>Gets the user's locale, if any.</summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Gets the tokens the provider issued, keyed by the names in <see cref="KyrolusAuthConstants.Tokens"/>.
    /// Empty when <see cref="KyrolusExternalLoginOptions.SaveTokens"/> is disabled.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Tokens { get; init; }
        = new Dictionary<string, string?>(StringComparer.Ordinal);
}
