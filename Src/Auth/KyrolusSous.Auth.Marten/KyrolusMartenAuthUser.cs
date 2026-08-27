using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Marten;

/// <summary>
/// Document entity stored in Marten representing an authenticated user with credentials and external logins.
/// </summary>
public sealed class KyrolusMartenAuthUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public string? DisplayName { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public IList<string> Roles { get; set; } = [];
    public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? TenantId { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; } = true;
    public string? SecurityStamp { get; set; }

    public IList<KyrolusMartenExternalLogin> ExternalLogins { get; set; } = [];

    public KyrolusAuthUser ToAuthUser()
    {
        return new KyrolusAuthUser
        {
            Id = Id,
            UserName = UserName,
            Email = Email,
            EmailConfirmed = EmailConfirmed,
            PhoneNumber = PhoneNumber,
            PhoneNumberConfirmed = PhoneNumberConfirmed,
            DisplayName = DisplayName,
            PasswordHash = PasswordHash,
            IsActive = IsActive,
            Roles = [.. Roles],
            Claims = new Dictionary<string, string>(Claims, StringComparer.Ordinal),
            TenantId = TenantId,
            AccessFailedCount = AccessFailedCount,
            LockoutEnd = LockoutEnd,
            LockoutEnabled = LockoutEnabled,
            SecurityStamp = SecurityStamp
        };
    }

    public void CopyFrom(KyrolusAuthUser user)
    {
        Id = user.Id;
        UserName = user.UserName;
        Email = user.Email;
        EmailConfirmed = user.EmailConfirmed;
        PhoneNumber = user.PhoneNumber;
        PhoneNumberConfirmed = user.PhoneNumberConfirmed;
        DisplayName = user.DisplayName;
        PasswordHash = user.PasswordHash;
        IsActive = user.IsActive;
        Roles = [.. user.Roles];
        Claims = new Dictionary<string, string>(user.Claims, StringComparer.Ordinal);
        TenantId = user.TenantId;
        AccessFailedCount = user.AccessFailedCount;
        LockoutEnd = user.LockoutEnd;
        LockoutEnabled = user.LockoutEnabled;
        SecurityStamp = user.SecurityStamp;
    }
}

public sealed class KyrolusMartenExternalLogin
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
}
