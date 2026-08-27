using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.Mfa;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Kyrolus Multi-Factor Authentication (MFA / 2FA / TOTP) services.
    /// </summary>
    public static IServiceCollection AddKyrolusMfa(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusTotpService, KyrolusTotpService>();
        services.TryAddSingleton<IKyrolusRecoveryCodeService, KyrolusRecoveryCodeService>();
        return services;
    }
}
