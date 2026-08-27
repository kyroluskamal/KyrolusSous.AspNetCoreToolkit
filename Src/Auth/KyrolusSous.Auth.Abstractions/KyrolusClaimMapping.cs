namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Maps a key in an external provider's JSON user payload to an internal application claim type.
/// </summary>
/// <param name="ExternalClaimType">The JSON key returned by the external identity provider (for example <c>"picture"</c>).</param>
/// <param name="InternalClaimType">The claim type written onto the local principal (for example <c>"avatar_url"</c>).</param>
public sealed record KyrolusClaimMapping(string ExternalClaimType, string InternalClaimType);
