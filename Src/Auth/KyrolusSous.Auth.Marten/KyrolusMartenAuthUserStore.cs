using KyrolusSous.Auth.Abstractions;
using Marten;

namespace KyrolusSous.Auth.Marten;

/// <summary>
/// Marten document store implementation of <see cref="IKyrolusAuthUserStore"/> and <see cref="IKyrolusAuthUserLockoutStore"/>.
/// </summary>
public class KyrolusMartenAuthUserStore : IKyrolusAuthUserStore, IKyrolusAuthUserLockoutStore
{
    private readonly IDocumentSession _session;

    public KyrolusMartenAuthUserStore(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async ValueTask<KyrolusAuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var doc = await _session.LoadAsync<KyrolusMartenAuthUser>(userId, cancellationToken);
        return doc?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var trimmed = userName.Trim();
        var doc = await _session.Query<KyrolusMartenAuthUser>()
            .FirstOrDefaultAsync(u => u.UserName == trimmed || u.UserName.ToLower() == trimmed.ToLower(), cancellationToken);

        return doc?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var trimmed = email.Trim();
        var doc = await _session.Query<KyrolusMartenAuthUser>()
            .FirstOrDefaultAsync(u => u.Email != null && (u.Email == trimmed || u.Email.ToLower() == trimmed.ToLower()), cancellationToken);

        return doc?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var doc = await _session.Query<KyrolusMartenAuthUser>()
            .FirstOrDefaultAsync(u => u.ExternalLogins.Any(l => l.Provider == provider && l.ProviderKey == providerKey), cancellationToken);

        return doc?.ToAuthUser();
    }

    public async ValueTask<KyrolusAuthUser> CreateAsync(KyrolusAuthUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            user.Id = Guid.NewGuid().ToString("N");
        }

        var doc = new KyrolusMartenAuthUser();
        doc.CopyFrom(user);

        _session.Store(doc);
        await _session.SaveChangesAsync(cancellationToken);

        return doc.ToAuthUser();
    }

    public async ValueTask AddExternalLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var doc = await _session.LoadAsync<KyrolusMartenAuthUser>(userId, cancellationToken);
        if (doc is null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' was not found.");
        }

        if (!doc.ExternalLogins.Any(l => l.Provider == provider && l.ProviderKey == providerKey))
        {
            doc.ExternalLogins.Add(new KyrolusMartenExternalLogin
            {
                Provider = provider,
                ProviderKey = providerKey
            });

            _session.Store(doc);
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask RecordFailedAttemptAsync(string userId, int accessFailedCount, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var doc = await _session.LoadAsync<KyrolusMartenAuthUser>(userId, cancellationToken);
        if (doc is not null)
        {
            doc.AccessFailedCount = Math.Max(0, accessFailedCount);
            doc.LockoutEnd = lockoutEnd;
            _session.Store(doc);
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask ResetFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var doc = await _session.LoadAsync<KyrolusMartenAuthUser>(userId, cancellationToken);
        if (doc is not null)
        {
            doc.AccessFailedCount = 0;
            doc.LockoutEnd = null;
            _session.Store(doc);
            await _session.SaveChangesAsync(cancellationToken);
        }
    }
}
