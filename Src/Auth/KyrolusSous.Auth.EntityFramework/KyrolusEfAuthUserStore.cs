using KyrolusSous.Auth.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Auth.EntityFramework;

/// <summary>
/// Entity Framework Core implementation of <see cref="IKyrolusAuthUserStore"/> and <see cref="IKyrolusAuthUserLockoutStore"/>.
/// </summary>
public class KyrolusEfAuthUserStore<TDbContext> : IKyrolusAuthUserStore, IKyrolusAuthUserLockoutStore
    where TDbContext : DbContext
{
    protected readonly TDbContext Db;

    public KyrolusEfAuthUserStore(TDbContext db)
    {
        Db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async ValueTask<KyrolusAuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var entity = await Db.Set<KyrolusEfAuthUserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return entity?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var trimmed = userName.Trim();
        var entity = await Db.Set<KyrolusEfAuthUserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == trimmed || u.UserName.ToLower() == trimmed.ToLower(), cancellationToken);

        return entity?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var trimmed = email.Trim();
        var entity = await Db.Set<KyrolusEfAuthUserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null && (u.Email == trimmed || u.Email.ToLower() == trimmed.ToLower()), cancellationToken);

        return entity?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var login = await Db.Set<KyrolusEfExternalLoginEntity>()
            .AsNoTracking()
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Provider == provider && l.ProviderKey == providerKey, cancellationToken);

        return login?.User?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser> CreateAsync(KyrolusAuthUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            user.Id = Guid.NewGuid().ToString("N");
        }

        var entity = new KyrolusEfAuthUserEntity();
        entity.CopyFrom(user);

        await Db.Set<KyrolusEfAuthUserEntity>().AddAsync(entity, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);

        return entity.ToAuthUser();
    }

    public async ValueTask AddExternalLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var exists = await Db.Set<KyrolusEfExternalLoginEntity>()
            .AnyAsync(l => l.UserId == userId && l.Provider == provider && l.ProviderKey == providerKey, cancellationToken);

        if (exists)
        {
            return;
        }

        var login = new KyrolusEfExternalLoginEntity
        {
            UserId = userId,
            Provider = provider,
            ProviderKey = providerKey
        };

        await Db.Set<KyrolusEfExternalLoginEntity>().AddAsync(login, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask RecordFailedAttemptAsync(string userId, int accessFailedCount, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var entity = await Db.Set<KyrolusEfAuthUserEntity>().FindAsync([userId], cancellationToken);
        if (entity is not null)
        {
            entity.AccessFailedCount = Math.Max(0, accessFailedCount);
            entity.LockoutEnd = lockoutEnd;
            await Db.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask ResetFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var entity = await Db.Set<KyrolusEfAuthUserEntity>().FindAsync([userId], cancellationToken);
        if (entity is not null)
        {
            entity.AccessFailedCount = 0;
            entity.LockoutEnd = null;
            await Db.SaveChangesAsync(cancellationToken);
        }
    }
}
