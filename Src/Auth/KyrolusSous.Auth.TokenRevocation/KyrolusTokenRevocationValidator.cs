using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.TokenRevocation;

public static class KyrolusTokenRevocationValidator
{
    /// <summary>
    /// Validates an authenticated claims principal against the token blacklist.
    /// </summary>
    public static async Task<bool> IsValidAsync(
        ClaimsPrincipal? principal,
        IKyrolusTokenBlacklist blacklist,
        CancellationToken cancellationToken = default)
    {
        if (principal?.Identity is not { IsAuthenticated: true })
        {
            return true;
        }

        // 1. Check jti claim
        var jti = principal.FindFirst("jti")?.Value
               ?? principal.FindFirst(ClaimTypes.SerialNumber)?.Value;

        if (!string.IsNullOrEmpty(jti) && await blacklist.IsTokenRevokedAsync(jti, cancellationToken))
        {
            return false;
        }

        // 2. Check user-wide revocation with iat claim
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? principal.FindFirst("sub")?.Value;

        var iatStr = principal.FindFirst("iat")?.Value;
        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(iatStr))
        {
            DateTimeOffset? issuedAt = null;
            if (long.TryParse(iatStr, out var iatSeconds))
            {
                issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatSeconds);
            }
            else if (double.TryParse(iatStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var iatDouble))
            {
                issuedAt = DateTimeOffset.FromUnixTimeSeconds((long)iatDouble);
            }
            else if (DateTimeOffset.TryParse(iatStr, out var iatParsed))
            {
                issuedAt = iatParsed;
            }

            if (issuedAt.HasValue && await blacklist.IsUserTokenRevokedAsync(userId, issuedAt.Value, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Configures <see cref="JwtBearerOptions"/> to reject revoked tokens automatically.
    /// </summary>
    public static JwtBearerOptions EnforceRevocation(this JwtBearerOptions options)
    {
        var existingHandler = options.Events?.OnTokenValidated;

        options.Events ??= new JwtBearerEvents();
        options.Events.OnTokenValidated = async context =>
        {
            if (existingHandler is not null)
            {
                await existingHandler(context);
                if (context.Result?.Failure is not null)
                {
                    return;
                }
            }

            var blacklist = context.HttpContext.RequestServices.GetRequiredService<IKyrolusTokenBlacklist>();
            var isValid = await IsValidAsync(context.Principal, blacklist, context.HttpContext.RequestAborted);

            if (!isValid)
            {
                context.Fail("Token has been revoked.");
            }
        };

        return options;
    }
}

