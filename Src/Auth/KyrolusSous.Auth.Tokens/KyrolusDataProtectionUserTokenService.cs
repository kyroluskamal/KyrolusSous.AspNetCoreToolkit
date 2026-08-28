using System.Text;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.Auth.Tokens;

/// <summary>
/// Enterprise implementation of <see cref="IKyrolusUserTokenService"/> integrating directly with ASP.NET Core Data Protection Key Ring.
/// Generates authenticated, tamper-proof, encrypted user tokens.
/// </summary>
public sealed class KyrolusDataProtectionUserTokenService(
    IDataProtectionProvider dataProtectionProvider,
    KyrolusUserTokenOptions? options = null) : IKyrolusUserTokenService
{
    private const string BasePurpose = "KyrolusSous.Auth.Tokens";
    private readonly IDataProtectionProvider _provider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
    private readonly KyrolusUserTokenOptions _options = options ?? new KyrolusUserTokenOptions();

    public string GenerateToken(KyrolusAuthUser user, string purpose, TimeSpan? customLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var lifetime = customLifetime ?? purpose switch
        {
            KyrolusTokenPurposes.PasswordReset => _options.PasswordResetLifetime,
            KyrolusTokenPurposes.EmailConfirmation => _options.EmailConfirmationLifetime,
            _ => _options.DefaultLifetime
        };

        var expiresUnixSeconds = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var stamp = user.SecurityStamp ?? string.Empty;

        // Payload format: EscapedUserId|ExpiresUnixSeconds|EscapedPurpose|EscapedSecurityStamp
        var payloadString = $"{Uri.EscapeDataString(user.Id)}|{expiresUnixSeconds}|{Uri.EscapeDataString(purpose)}|{Uri.EscapeDataString(stamp)}";
        var payloadBytes = Encoding.UTF8.GetBytes(payloadString);

        var protector = _provider.CreateProtector($"{BasePurpose}:{purpose}");
        var protectedBytes = protector.Protect(payloadBytes);

        return Convert.ToBase64String(protectedBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public bool ValidateToken(KyrolusAuthUser user, string purpose, string token)
    {
        if (user is null || string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var protectedBytes = Convert.FromBase64String(padded);
            var protector = _provider.CreateProtector($"{BasePurpose}:{purpose}");
            var payloadBytes = protector.Unprotect(protectedBytes);
            var payloadString = Encoding.UTF8.GetString(payloadBytes);

            var parts = payloadString.Split('|');
            if (parts.Length != 4)
            {
                return false;
            }

            var userId = Uri.UnescapeDataString(parts[0]);
            if (!long.TryParse(parts[1], out var expiresUnixSeconds))
            {
                return false;
            }

            var payloadPurpose = Uri.UnescapeDataString(parts[2]);
            var stamp = Uri.UnescapeDataString(parts[3]);

            if (!string.Equals(userId, user.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(payloadPurpose, purpose, StringComparison.Ordinal))
            {
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnixSeconds)
            {
                return false;
            }

            var currentStamp = user.SecurityStamp ?? string.Empty;
            if (!string.Equals(stamp, currentStamp, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
