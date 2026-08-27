namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// A plain, persistence-ignorant user record. It is deliberately a simple mutable class with no
/// base type, no attributes and no navigation properties, so it can be projected from an EF Core
/// entity, a Marten document, a Dapper row or a REST call with equal ease.
/// </summary>
public sealed class KyrolusAuthUser
{
    /// <summary>Gets or sets the stable user identifier (the <c>sub</c> claim of issued tokens).</summary>
    public string Id { get; set; } = "";

    /// <summary>Gets or sets the login name.</summary>
    public string UserName { get; set; } = "";

    /// <summary>Gets or sets the email address.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets whether the email address has been confirmed.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Gets or sets whether the phone number has been confirmed.</summary>
    public bool PhoneNumberConfirmed { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the stored password hash. <c>null</c> for accounts that only ever sign in
    /// through an external provider.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>Gets or sets whether the account is enabled. Disabled accounts cannot obtain tokens.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the roles granted to this user.</summary>
    public IList<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets extra claims to embed in issued tokens, keyed by claim type.
    /// </summary>
    public IDictionary<string, string> Claims { get; set; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the tenant this user belongs to, for multi-tenant applications.</summary>
    public string? TenantId { get; set; }

    /// <summary>Gets or sets the number of consecutive failed sign-in attempts.</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>
    /// Gets or sets the instant the current lockout expires. A value in the future means the
    /// account is locked out right now.
    /// </summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>Gets or sets whether this account participates in lockout at all.</summary>
    public bool LockoutEnabled { get; set; } = true;

    /// <summary>Gets or sets the security stamp, bumped whenever credentials change to invalidate old tokens.</summary>
    public string? SecurityStamp { get; set; }

    /// <summary>Gets whether the account is locked out at <paramref name="now"/>.</summary>
    /// <param name="now">The instant to evaluate against; pass <c>TimeProvider.System.GetUtcNow()</c>.</param>
    public bool IsLockedOut(DateTimeOffset now)
        => LockoutEnabled && LockoutEnd is { } end && end > now;

    /// <summary>
    /// Safely adds a role to the user, ensuring whitespace is trimmed, empty strings are ignored, and duplicates are prevented.
    /// </summary>
    /// <param name="role">The role name to add.</param>
    public void AddRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return;
        var trimmed = role.Trim();
        if (!Roles.Any(r => string.Equals(r, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            Roles.Add(trimmed);
        }
    }
}
