using System.Text.Json;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.MagicLink;
using KyrolusSous.Auth.Sessions;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Auth.EntityFramework;

public class KyrolusEfAuthUserEntity
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
    public string RolesJson { get; set; } = "[]";
    public string ClaimsJson { get; set; } = "{}";
    public string? TenantId { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; } = true;
    public string? SecurityStamp { get; set; }

    public ICollection<KyrolusEfExternalLoginEntity> ExternalLogins { get; set; } = new List<KyrolusEfExternalLoginEntity>();

    public KyrolusAuthUser ToAuthUser()
    {
        var roles = JsonSerializer.Deserialize<List<string>>(RolesJson) ?? [];
        var claims = JsonSerializer.Deserialize<Dictionary<string, string>>(ClaimsJson) ?? [];

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
            Roles = roles,
            Claims = claims,
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
        RolesJson = JsonSerializer.Serialize(user.Roles);
        ClaimsJson = JsonSerializer.Serialize(user.Claims);
        TenantId = user.TenantId;
        AccessFailedCount = user.AccessFailedCount;
        LockoutEnd = user.LockoutEnd;
        LockoutEnabled = user.LockoutEnabled;
        SecurityStamp = user.SecurityStamp;
    }
}

public class KyrolusEfExternalLoginEntity
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;

    public KyrolusEfAuthUserEntity? User { get; set; }
}

public class KyrolusEfRevokedTokenEntity
{
    public string Jti { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public class KyrolusEfUserRevocationEntity
{
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset RevokedBeforeUtc { get; set; }
}

public class KyrolusEfSessionEntity
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    public KyrolusUserSession ToSession()
    {
        return new KyrolusUserSession
        {
            SessionId = SessionId,
            UserId = UserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent,
            DeviceInfo = DeviceInfo,
            CreatedAt = CreatedAt,
            LastActiveAt = LastActiveAt,
            ExpiresAt = ExpiresAt,
            IsRevoked = IsRevoked
        };
    }

    public static KyrolusEfSessionEntity FromSession(KyrolusUserSession s)
    {
        return new KyrolusEfSessionEntity
        {
            SessionId = s.SessionId,
            UserId = s.UserId,
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            DeviceInfo = s.DeviceInfo,
            CreatedAt = s.CreatedAt,
            LastActiveAt = s.LastActiveAt,
            ExpiresAt = s.ExpiresAt,
            IsRevoked = s.IsRevoked
        };
    }
}

public class KyrolusEfMagicLinkEntity
{
    public string TokenHash { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsConsumed { get; set; }

    public KyrolusMagicLinkRecord ToRecord()
    {
        return new KyrolusMagicLinkRecord(TokenHash, UserId, Email, ExpiresAtUtc);
    }
}

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configures the Entity Framework Core model with Kyrolus auth tables and indexes.
    /// </summary>
    public static ModelBuilder ApplyKyrolusAuthConfig(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KyrolusEfAuthUserEntity>(b =>
        {
            b.HasKey(u => u.Id);
            b.HasIndex(u => u.UserName).IsUnique();
            b.HasIndex(u => u.Email);
            b.Property(u => u.UserName).HasMaxLength(256).IsRequired();
            b.Property(u => u.Email).HasMaxLength(256);
            b.Property(u => u.DisplayName).HasMaxLength(256);
            b.HasMany(u => u.ExternalLogins)
                .WithOne(l => l.User)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KyrolusEfExternalLoginEntity>(b =>
        {
            b.HasKey(l => l.Id);
            b.HasIndex(l => new { l.Provider, l.ProviderKey }).IsUnique();
            b.Property(l => l.Provider).HasMaxLength(64).IsRequired();
            b.Property(l => l.ProviderKey).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<KyrolusEfRevokedTokenEntity>(b =>
        {
            b.HasKey(t => t.Jti);
            b.HasIndex(t => t.ExpiresAtUtc);
        });

        modelBuilder.Entity<KyrolusEfUserRevocationEntity>(b =>
        {
            b.HasKey(u => u.UserId);
        });

        modelBuilder.Entity<KyrolusEfSessionEntity>(b =>
        {
            b.HasKey(s => s.SessionId);
            b.HasIndex(s => s.UserId);
            b.HasIndex(s => s.ExpiresAt);
        });

        modelBuilder.Entity<KyrolusEfMagicLinkEntity>(b =>
        {
            b.HasKey(m => m.TokenHash);
            b.HasIndex(m => m.ExpiresAtUtc);
        });

        return modelBuilder;
    }
}
