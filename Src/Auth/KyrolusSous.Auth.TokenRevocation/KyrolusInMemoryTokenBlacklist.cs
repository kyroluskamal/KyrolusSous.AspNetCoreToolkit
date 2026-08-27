using System.Collections.Concurrent;

namespace KyrolusSous.Auth.TokenRevocation;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IKyrolusTokenBlacklist"/> with lazy expiration cleanup.
/// </summary>
public sealed class KyrolusInMemoryTokenBlacklist : IKyrolusTokenBlacklist
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revokedJtis = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _userRevocations = new(StringComparer.Ordinal);

    public Task RevokeTokenAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        if (expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Task.CompletedTask;
        }

        _revokedJtis[jti.Trim()] = expiresAtUtc;
        return Task.CompletedTask;
    }

    public Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return Task.FromResult(false);
        }

        if (_revokedJtis.TryGetValue(jti, out var expiresAtUtc))
        {
            if (DateTimeOffset.UtcNow < expiresAtUtc)
            {
                return Task.FromResult(true);
            }

            // Clean up expired entry
            _revokedJtis.TryRemove(jti, out _);
        }

        return Task.FromResult(false);
    }

    public Task RevokeUserTokensAsync(string userId, DateTimeOffset revokedBeforeUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        _userRevocations.AddOrUpdate(
            userId,
            revokedBeforeUtc,
            (_, existing) => revokedBeforeUtc > existing ? revokedBeforeUtc : existing);

        return Task.CompletedTask;
    }

    public Task<bool> IsUserTokenRevokedAsync(string userId, DateTimeOffset tokenIssuedAtUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(false);
        }

        if (_userRevocations.TryGetValue(userId, out var revokedBeforeUtc))
        {
            return Task.FromResult(tokenIssuedAtUtc.ToUnixTimeSeconds() <= revokedBeforeUtc.ToUnixTimeSeconds());
        }

        return Task.FromResult(false);
    }

    public Task<int> PurgeExpiredRevocationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var count = 0;
        foreach (var (jti, expiresAt) in _revokedJtis.ToArray())
        {
            if (now >= expiresAt)
            {
                if (_revokedJtis.TryRemove(jti, out _))
                {
                    count++;
                }
            }
        }
        return Task.FromResult(count);
    }
}
