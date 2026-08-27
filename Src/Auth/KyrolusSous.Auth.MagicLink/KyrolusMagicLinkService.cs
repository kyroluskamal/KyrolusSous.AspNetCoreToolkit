using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.MagicLink;

/// <summary>
/// Thread-safe in-memory store for magic link tokens with atomic single-use consumption.
/// </summary>
public sealed class KyrolusInMemoryMagicLinkStore : IKyrolusMagicLinkStore
{
    private readonly ConcurrentDictionary<string, KyrolusMagicLinkRecord> _tokens = new(StringComparer.Ordinal);

    public Task SaveTokenAsync(string tokenHash, string userId, string email, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        _tokens[tokenHash] = new KyrolusMagicLinkRecord(tokenHash, userId, email, expiresAtUtc);
        return Task.CompletedTask;
    }

    public Task<KyrolusMagicLinkRecord?> ConsumeTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        _tokens.TryRemove(tokenHash, out var record);
        return Task.FromResult(record);
    }

    public Task<int> PurgeExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var count = 0;
        foreach (var (key, record) in _tokens)
        {
            if (now > record.ExpiresAtUtc)
            {
                if (_tokens.TryRemove(key, out _))
                {
                    count++;
                }
            }
        }
        return Task.FromResult(count);
    }
}

/// <summary>
/// Implementation of <see cref="IKyrolusMagicLinkService"/> generating 256-bit cryptographically secure single-use magic links.
/// </summary>
public sealed class KyrolusMagicLinkService : IKyrolusMagicLinkService
{
    private readonly IKyrolusMagicLinkStore _store;
    private readonly KyrolusMagicLinkOptions _options;

    public KyrolusMagicLinkService(IKyrolusMagicLinkStore store, KyrolusMagicLinkOptions? options = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new KyrolusMagicLinkOptions();
    }

    public async Task<KyrolusMagicLinkCreationResult> CreateMagicLinkAsync(
        KyrolusAuthUser user,
        string baseCallbackUrl,
        TimeSpan? customLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseCallbackUrl);

        var recipient = !string.IsNullOrWhiteSpace(user.Email) ? user.Email.Trim() : user.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("User must have a non-empty Email or UserName.", nameof(user));
        }

        if (recipient.Any(c => c == '\r' || c == '\n'))
        {
            throw new ArgumentException("Recipient identifier contains invalid carriage return or newline characters.", nameof(user));
        }

        var randomBytes = new byte[32]; // 256 bits entropy
        RandomNumberGenerator.Fill(randomBytes);
        var rawToken = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var tokenHash = HashToken(rawToken);
        var lifetime = customLifetime ?? _options.TokenLifetime;
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(lifetime);

        await _store.SaveTokenAsync(tokenHash, user.Id, recipient, expiresAtUtc, cancellationToken);

        string magicLinkUrl;
        if (Uri.TryCreate(baseCallbackUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var builder = new UriBuilder(uri);
            var queryToAppend = $"{_options.TokenQueryParam}={Uri.EscapeDataString(rawToken)}";
            builder.Query = string.IsNullOrEmpty(builder.Query) || builder.Query == "?"
                ? queryToAppend
                : $"{builder.Query.TrimStart('?')}&{queryToAppend}";
            magicLinkUrl = builder.Uri.ToString();
        }
        else
        {
            throw new ArgumentException("Base callback URL must be a valid absolute HTTP or HTTPS URL.", nameof(baseCallbackUrl));
        }

        return new KyrolusMagicLinkCreationResult(rawToken, magicLinkUrl, expiresAtUtc);
    }

    public async Task<KyrolusMagicLinkValidationResult> ValidateAndConsumeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 256)
        {
            return KyrolusMagicLinkValidationResult.Failed("Token is invalid or malformed.");
        }

        var tokenHash = HashToken(rawToken.Trim());
        var record = await _store.ConsumeTokenAsync(tokenHash, cancellationToken);

        if (record is null)
        {
            return KyrolusMagicLinkValidationResult.Failed("Invalid or already consumed magic link.");
        }

        if (DateTimeOffset.UtcNow > record.ExpiresAtUtc)
        {
            return KyrolusMagicLinkValidationResult.Failed("Magic link has expired.");
        }

        return KyrolusMagicLinkValidationResult.Success(record.UserId, record.Email);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
