using System.Collections.Concurrent;
using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// An in-memory <see cref="IKyrolusAuthUserStore"/> for local development, samples and tests.
/// Everything it holds is lost when the process exits.
/// </summary>
/// <remarks>
/// This exists so a new project can get an end-to-end token flow running before deciding on a
/// database - not as a production store. Register it explicitly; nothing wires it up by default.
/// </remarks>
public sealed class KyrolusInMemoryAuthUserStore : IKyrolusAuthUserStore, IKyrolusAuthUserLockoutStore
{
    private readonly ConcurrentDictionary<string, KyrolusAuthUser> _users = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _externalLogins = new(StringComparer.Ordinal);

    /// <summary>Gets every user currently held, in no particular order.</summary>
    public IReadOnlyCollection<KyrolusAuthUser> Users => [.. _users.Values];

    /// <summary>
    /// Adds a user, assigning an id when the record does not carry one.
    /// </summary>
    /// <param name="user">The user to add.</param>
    /// <returns>The stored user.</returns>
    public KyrolusAuthUser Add(KyrolusAuthUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            user.Id = Guid.NewGuid().ToString("N");
        }

        _users[user.Id] = user;
        return user;
    }

    /// <inheritdoc />
    public ValueTask<KyrolusAuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ValueTask.FromResult<KyrolusAuthUser?>(null);
        }

        return ValueTask.FromResult(_users.TryGetValue(userId.Trim(), out var user) ? user : null);
    }

    /// <inheritdoc />
    public ValueTask<KyrolusAuthUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Find(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc />
    public ValueTask<KyrolusAuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Find(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc />
    public ValueTask<KyrolusAuthUser?> FindByExternalLoginAsync(
        string provider,
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        if (!_externalLogins.TryGetValue(LoginKey(provider, providerKey), out var userId))
        {
            return ValueTask.FromResult<KyrolusAuthUser?>(null);
        }

        return FindByIdAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<KyrolusAuthUser> CreateAsync(KyrolusAuthUser user, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Add(user));

    /// <inheritdoc />
    public ValueTask AddExternalLoginAsync(
        string userId,
        string provider,
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        _externalLogins[LoginKey(provider, providerKey)] = userId;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RecordFailedAttemptAsync(
        string userId,
        int accessFailedCount,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken = default)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.AccessFailedCount = accessFailedCount;
            user.LockoutEnd = lockoutEnd;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResetFailedAttemptsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
        }

        return ValueTask.CompletedTask;
    }

    private KyrolusAuthUser? Find(Func<KyrolusAuthUser, bool> predicate)
    {
        foreach (var user in _users.Values)
        {
            if (predicate(user))
            {
                return user;
            }
        }

        return null;
    }

    // Separated by a unit separator rather than plainly concatenated: "Goog" + "le1" and
    // "Google" + "1" must not collapse onto the same key.
    private static string LoginKey(string provider, string providerKey) => $"{provider}{providerKey}";
}
