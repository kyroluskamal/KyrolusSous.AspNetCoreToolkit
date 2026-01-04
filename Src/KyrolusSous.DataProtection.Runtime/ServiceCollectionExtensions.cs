using System.Security.Cryptography.X509Certificates;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtection(
        this IServiceCollection services,
        Action<KyrolusDataProtectionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KyrolusDataProtectionOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IValidateOptions<KyrolusDataProtectionOptions>, KyrolusDataProtectionOptionsValidator>();
        services.AddOptions<KyrolusDataProtectionOptions>()
            .Configure(o =>
        {
            o.ApplicationName = options.ApplicationName;
            o.DefaultKeyLifetime = options.DefaultKeyLifetime;
            o.AutoGenerateKeys = options.AutoGenerateKeys;
            o.KeyProtection = options.KeyProtection;
        })
        .ValidateOnStart();

        var builder = services
            .AddDataProtection()
            .SetApplicationName(options.ApplicationName);

        if (options.DefaultKeyLifetime.HasValue)
            builder.SetDefaultKeyLifetime(options.DefaultKeyLifetime.Value);

        if (options.AutoGenerateKeys.HasValue)
            builder.AddKeyManagementOptions(o => o.AutoGenerateKeys = options.AutoGenerateKeys.Value);

        ApplyKeyProtection(builder, options);

        services.TryAddSingleton<IKyrolusDataProtectionKeyManager, KyrolusDataProtectionKeyManager>();
        services.TryAddSingleton<IKyrolusDataProtectionKeyRepository, KyrolusDataProtectionKeyRepository>();
        services.TryAddSingleton<IKyrolusDataProtectorFactory, KyrolusDataProtectorFactory>();
        services.TryAddSingleton<IKyrolusTenantDataProtectionProvider, KyrolusTenantDataProtectionProvider>();
        services.TryAddSingleton<KyrolusDataProtectionKeyBackupService>();
        services.TryAddSingleton<KyrolusDataProtectionInstanceId>();
        services.TryAddSingleton<KyrolusKeyRingRefreshTokenSource>();
        services.TryAddSingleton<IKyrolusKeyRingRefreshNotifier, KyrolusNullKeyRingRefreshNotifier>();
        services.AddOptions<KyrolusDataProtectionTenantOptions>();
        services.AddOptions<KyrolusDataProtectionInstrumentationOptions>();
        services.AddOptions<KyrolusDataProtectionKeyCleanupOptions>();
        services.AddOptions<KyrolusDataProtectionKeyRingRefreshOptions>();
        DecorateKeyManager(services);

        return new KyrolusDataProtectionBuilder(services, builder, options);
    }

    private static void ApplyKeyProtection(IDataProtectionBuilder builder, KyrolusDataProtectionOptions options)
    {
        var protection = options.KeyProtection;
        if (protection == null || protection.Kind == KyrolusKeyProtectionKind.None)
        {
            return;
        }

        if (protection.Kind == KyrolusKeyProtectionKind.Dpapi)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI key protection is only supported on Windows.");
            }

            builder.ProtectKeysWithDpapi(protection.UseMachineStore);
            return;
        }

        if (protection.Kind == KyrolusKeyProtectionKind.Certificate)
        {
            var certificate = LoadCertificate(protection);
            builder.ProtectKeysWithCertificate(certificate);
        }
    }

    private static X509Certificate2 LoadCertificate(KyrolusKeyProtectionOptions protection)
    {
        if (string.IsNullOrWhiteSpace(protection.CertificateThumbprint))
        {
            throw new InvalidOperationException(
                "Certificate thumbprint is required when using certificate key protection.");
        }

        using var store = new X509Store(protection.StoreName, protection.StoreLocation);
        store.Open(OpenFlags.ReadOnly);

        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            protection.CertificateThumbprint,
            validOnly: false);

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Certificate '{protection.CertificateThumbprint}' not found in {protection.StoreLocation}/{protection.StoreName}.");
        }

        return matches[0];
    }

    private static void DecorateKeyManager(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IKeyManager));
        if (descriptor is null)
            return;

        if (descriptor.ImplementationType == typeof(KyrolusKeyManagerRefreshDecorator))
            return;

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(IKeyManager), sp =>
        {
            var inner = (IKeyManager)CreateInstance(sp, descriptor);
            return new KyrolusKeyManagerRefreshDecorator(
                inner,
                sp.GetRequiredService<KyrolusKeyRingRefreshTokenSource>(),
                sp.GetRequiredService<IKyrolusKeyRingRefreshNotifier>(),
                sp.GetRequiredService<IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions>>(),
                sp.GetRequiredService<IOptions<KyrolusDataProtectionOptions>>(),
                sp.GetRequiredService<KyrolusDataProtectionInstanceId>(),
                sp.GetRequiredService<ILogger<KyrolusKeyManagerRefreshDecorator>>());
        }, descriptor.Lifetime));
    }

    private static object CreateInstance(IServiceProvider services, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
            return descriptor.ImplementationInstance;

        if (descriptor.ImplementationFactory is not null)
            return descriptor.ImplementationFactory(services);

        if (descriptor.ImplementationType is null)
            throw new InvalidOperationException("IKeyManager registration has no implementation.");

        return ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType);
    }
}
