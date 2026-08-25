namespace KyrolusSous.CQRS.Abstractions.Security;

/// <summary>
/// Exception thrown when a CQRS request fails authorization checks.
/// </summary>
public class KyrolusSecurityException : Exception
{
    /// <summary>
    /// Gets the missing role, policy, or permission that caused the failure.
    /// </summary>
    public string? RequiredClaim { get; }

    public KyrolusSecurityException(string message, string? requiredClaim = null)
        : base(message)
    {
        RequiredClaim = requiredClaim;
    }

    public KyrolusSecurityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
