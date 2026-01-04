namespace KyrolusSous.DataProtection.Abstractions;

public enum KyrolusKeyRingRefreshReason
{
    Unknown = 0,
    KeyCreated = 1,
    KeyRevoked = 2,
    KeyRotated = 3,
    Cleanup = 4
}
