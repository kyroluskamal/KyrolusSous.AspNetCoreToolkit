using System.Security.Claims;

namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// The outcome of handling an external login: either the local identity to sign in,
/// or a reason the sign-in was refused.
/// </summary>
public sealed class KyrolusExternalLoginResult
{
    private KyrolusExternalLoginResult(
        bool succeeded,
        ClaimsPrincipal? principal,
        IReadOnlyList<Claim> additionalClaims,
        string? errorCode,
        string? errorDescription)
    {
        Succeeded = succeeded;
        Principal = principal;
        AdditionalClaims = additionalClaims;
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
    }

    /// <summary>Gets whether the external login was accepted.</summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the principal to sign in, when the handler chose to replace the provider's principal
    /// outright. <c>null</c> means "keep the provider's principal and merge
    /// <see cref="AdditionalClaims"/> into it".
    /// </summary>
    public ClaimsPrincipal? Principal { get; }

    /// <summary>
    /// Gets claims to merge into the provider's principal (local user id, roles, tenant, ...).
    /// </summary>
    public IReadOnlyList<Claim> AdditionalClaims { get; }

    /// <summary>Gets the error code when <see cref="Succeeded"/> is <c>false</c>.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets the human-readable failure reason when <see cref="Succeeded"/> is <c>false</c>.</summary>
    public string? ErrorDescription { get; }

    /// <summary>Accepts the login and keeps the provider's principal unchanged.</summary>
    public static KyrolusExternalLoginResult Success()
        => new(true, null, [], null, null);

    /// <summary>Accepts the login and merges <paramref name="additionalClaims"/> into the provider's principal.</summary>
    public static KyrolusExternalLoginResult Success(IReadOnlyList<Claim> additionalClaims)
        => new(true, null, additionalClaims ?? [], null, null);

    /// <summary>Accepts the login and replaces the provider's principal with <paramref name="principal"/>.</summary>
    public static KyrolusExternalLoginResult Success(ClaimsPrincipal principal)
        => new(true, principal, [], null, null);

    /// <summary>Rejects the login.</summary>
    public static KyrolusExternalLoginResult Fail(string errorCode, string? errorDescription = null)
        => new(false, null, [], errorCode, errorDescription);
}
