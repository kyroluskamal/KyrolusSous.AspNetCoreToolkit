namespace KyrolusSous.Auth.Events;

/// <summary>
/// Marker interface for all authentication and security audit events.
/// </summary>
public interface IKyrolusAuthEvent
{
    DateTimeOffset Timestamp { get; }
    string EventType { get; }
    string? UserId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public abstract record KyrolusAuthEventBase(
    string? UserId = null,
    string? IpAddress = null,
    string? UserAgent = null) : IKyrolusAuthEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public abstract string EventType { get; }
}

public sealed record KyrolusUserLoggedInEvent(
    string UserId,
    string UserName,
    string AuthMethod,
    string? IpAddress = null,
    string? UserAgent = null) : KyrolusAuthEventBase(UserId, IpAddress, UserAgent)
{
    public override string EventType => "UserLoggedIn";
}

public sealed record KyrolusUserLoginFailedEvent : KyrolusAuthEventBase
{
    public string AttemptedIdentifier { get; init; }
    public string Reason { get; init; }

    public KyrolusUserLoginFailedEvent(
        string attemptedIdentifier,
        string reason,
        string? ipAddress = null,
        string? userAgent = null) : base(null, ipAddress, userAgent)
    {
        AttemptedIdentifier = attemptedIdentifier?.Length > 256 ? attemptedIdentifier[..256] : (attemptedIdentifier ?? string.Empty);
        Reason = reason ?? string.Empty;
    }

    public override string EventType => "UserLoginFailed";
}

public sealed record KyrolusAccountLockedEvent(
    string UserId,
    int FailedCount,
    DateTimeOffset LockoutEnd,
    string? IpAddress = null) : KyrolusAuthEventBase(UserId, IpAddress)
{
    public override string EventType => "AccountLocked";
}

public sealed record KyrolusPasswordChangedEvent(
    string UserId,
    string? IpAddress = null) : KyrolusAuthEventBase(UserId, IpAddress)
{
    public override string EventType => "PasswordChanged";
}

public sealed record KyrolusPasswordResetRequestedEvent(
    string UserId,
    string Email,
    string? IpAddress = null) : KyrolusAuthEventBase(UserId, IpAddress)
{
    public override string EventType => "PasswordResetRequested";
}

public sealed record KyrolusTokenRevokedEvent : KyrolusAuthEventBase
{
    public string Jti { get; init; }
    public string? Reason { get; init; }

    public KyrolusTokenRevokedEvent(
        string jti,
        string? userId = null,
        string? reason = null) : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        Jti = jti;
        Reason = reason;
    }

    public override string EventType => "TokenRevoked";
}

public sealed record KyrolusImpersonationStartedEvent(
    string AdminId,
    string TargetUserId,
    string? Reason = null) : KyrolusAuthEventBase(TargetUserId)
{
    public override string EventType => "ImpersonationStarted";
}

public sealed record KyrolusMfaVerifiedEvent(
    string UserId,
    string Method,
    string? IpAddress = null) : KyrolusAuthEventBase(UserId, IpAddress)
{
    public override string EventType => "MfaVerified";
}
