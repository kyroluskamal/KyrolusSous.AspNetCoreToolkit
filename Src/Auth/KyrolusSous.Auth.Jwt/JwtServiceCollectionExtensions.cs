using System.Text;
using KyrolusSous.Auth.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace KyrolusSous.Auth.Jwt;

/// <summary>
/// Service collection extensions for configuring Kyrolus JWT token authentication.
/// </summary>
public static class JwtServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures Kyrolus JWT token services and authentication handler.
    /// </summary>
    public static AuthenticationBuilder AddKyrolusJwtAuth(
        this IServiceCollection services,
        Action<KyrolusJwtOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusJwtOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IKyrolusJwtTokenService, KyrolusJwtTokenService>();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));

        return services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = options.ValidateIssuerSigningKey,
                    IssuerSigningKey = key,
                    ValidateIssuer = options.ValidateIssuer,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = options.ValidateAudience,
                    ValidAudience = options.Audience,
                    ValidateLifetime = options.ValidateLifetime,
                    ClockSkew = options.ClockSkew
                };
            });
    }

    /// <summary>
    /// Adds only the token generator and validator service without registering the ASP.NET Core authentication middleware.
    /// </summary>
    public static IServiceCollection AddKyrolusJwtTokenService(
        this IServiceCollection services,
        Action<KyrolusJwtOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusJwtOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IKyrolusJwtTokenService, KyrolusJwtTokenService>();

        return services;
    }
}
