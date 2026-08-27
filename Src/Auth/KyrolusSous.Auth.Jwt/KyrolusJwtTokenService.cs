using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KyrolusSous.Auth.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace KyrolusSous.Auth.Jwt;

/// <summary>
/// High-performance, AOT-friendly implementation of <see cref="IKyrolusJwtTokenService"/>
/// using <see cref="JsonWebTokenHandler"/>.
/// </summary>
public sealed class KyrolusJwtTokenService : IKyrolusJwtTokenService
{
    private readonly KyrolusJwtOptions _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public KyrolusJwtTokenService(KyrolusJwtOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.SecretKey) || _options.SecretKey.Length < 32)
        {
            throw new ArgumentException("SecretKey must be at least 32 characters (256 bits) long.", nameof(options));
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
    }

    public string GenerateAccessToken(KyrolusAuthUser user, IEnumerable<Claim>? additionalClaims = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Id);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName ?? user.UserName ?? user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
            claims.Add(new Claim("email_verified", user.EmailConfirmed ? "true" : "false", ClaimValueTypes.Boolean));
        }

        if (!string.IsNullOrEmpty(user.TenantId))
        {
            claims.Add(new Claim("tenant_id", user.TenantId));
        }

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var (key, value) in user.Claims)
        {
            claims.Add(new Claim(key, value));
        }

        if (additionalClaims is not null)
        {
            var singleValuedCoreClaimTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                JwtRegisteredClaimNames.Sub,
                JwtRegisteredClaimNames.Name,
                JwtRegisteredClaimNames.Jti,
                JwtRegisteredClaimNames.Iat,
                JwtRegisteredClaimNames.Email,
                "email_verified",
                "tenant_id"
            };

            foreach (var claim in additionalClaims)
            {
                if (!singleValuedCoreClaimTypes.Contains(claim.Type))
                {
                    claims.Add(claim);
                }
            }
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(_options.AccessTokenLifetime),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        };

        return _tokenHandler.CreateToken(descriptor);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var bytes = Encoding.UTF8.GetBytes(refreshToken.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public bool VerifyRefreshToken(string rawRefreshToken, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        if (rawRefreshToken.Length > 2048 || storedHash.Length > 256)
        {
            return false;
        }

        var rawHash = HashRefreshToken(rawRefreshToken);
        var rawBytes = Encoding.UTF8.GetBytes(rawHash.ToUpperInvariant());
        var storedBytes = Encoding.UTF8.GetBytes(storedHash.Trim().ToUpperInvariant());

        return CryptographicOperations.FixedTimeEquals(rawBytes, storedBytes);
    }

    public async Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var cleanToken = NormalizeToken(token);
        if (cleanToken is null)
        {
            return null;
        }

        var validationParameters = CreateValidationParameters();
        var result = await _tokenHandler.ValidateTokenAsync(cleanToken, validationParameters).ConfigureAwait(false);
        return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var cleanToken = NormalizeToken(token);
        if (cleanToken is null)
        {
            return null;
        }

        var validationParameters = CreateValidationParameters();
        var result = _tokenHandler.ValidateTokenAsync(cleanToken, validationParameters).GetAwaiter().GetResult();
        return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
    }

    private static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var clean = token.Trim();
        if (clean.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(clean) || clean.Length > 8192)
        {
            return null;
        }

        return clean;
    }

    private TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuerSigningKey = _options.ValidateIssuerSigningKey,
        IssuerSigningKey = _signingKey,
        ValidateIssuer = _options.ValidateIssuer,
        ValidIssuer = _options.Issuer?.Trim(),
        ValidateAudience = _options.ValidateAudience,
        ValidAudience = _options.Audience?.Trim(),
        ValidateLifetime = _options.ValidateLifetime,
        ClockSkew = _options.ClockSkew
    };
}
