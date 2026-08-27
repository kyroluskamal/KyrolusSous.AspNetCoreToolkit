namespace KyrolusSous.Auth.MagicLink;

public sealed record KyrolusMagicLinkRecord(
    string TokenHash,
    string UserId,
    string Email,
    DateTimeOffset ExpiresAtUtc);

public sealed record KyrolusMagicLinkCreationResult(
    string RawToken,
    string MagicLinkUrl,
    DateTimeOffset ExpiresAtUtc);

public sealed class KyrolusMagicLinkValidationResult
{
    public bool Succeeded { get; private init; }
    public string? UserId { get; private init; }
    public string? Email { get; private init; }
    public string? FailureReason { get; private init; }

    public static KyrolusMagicLinkValidationResult Success(string userId, string email)
        => new() { Succeeded = true, UserId = userId, Email = email };

    public static KyrolusMagicLinkValidationResult Failed(string reason)
        => new() { Succeeded = false, FailureReason = reason };
}

public sealed class KyrolusMagicLinkOptions
{
    /// <summary>
    /// Lifetime of a magic link. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Name of the query parameter appended to the callback URL. Defaults to <c>token</c>.
    /// </summary>
    public string TokenQueryParam { get; set; } = "token";
}
