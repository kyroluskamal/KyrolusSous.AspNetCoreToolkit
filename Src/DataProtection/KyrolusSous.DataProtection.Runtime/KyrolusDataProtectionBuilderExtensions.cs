using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace KyrolusSous.DataProtection.Runtime;

public static class KyrolusDataProtectionBuilderExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionInstrumentation(
        this KyrolusDataProtectionBuilder builder,
        Action<KyrolusDataProtectionInstrumentationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddSingleton<KyrolusDataProtectionInstrumentation>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IKyrolusDataProtectorFactory, KyrolusInstrumentedDataProtectorFactory>());
        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionHealthChecks(
        this KyrolusDataProtectionBuilder builder,
        string name = "kyrolus-dataprotection",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddHealthChecks()
            .AddCheck<KyrolusDataProtectionHealthCheck>(
                name,
                failureStatus,
                tags ?? []);

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionTenantIsolation(
        this KyrolusDataProtectionBuilder builder,
        Action<KyrolusDataProtectionTenantOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionFileKeyEscrow(
        this KyrolusDataProtectionBuilder builder,
        string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.DataProtection.AddKeyManagementOptions(o =>
        {
            o.KeyEscrowSinks.Add(new KyrolusFileKeyEscrowSink(directoryPath));
        });

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionFileKeyEscrowEncrypted(
        this KyrolusDataProtectionBuilder builder,
        string directoryPath,
        Action<KyrolusDataProtectionKeyEscrowEncryptionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new KyrolusDataProtectionKeyEscrowEncryptionOptions();
        configure?.Invoke(options);

        builder.DataProtection.AddKeyManagementOptions(o =>
        {
            o.KeyEscrowSinks.Add(new KyrolusEncryptedFileKeyEscrowSink(directoryPath, options));
        });

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionKeyCleanup(
        this KyrolusDataProtectionBuilder builder,
        Action<KyrolusDataProtectionKeyCleanupOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<KyrolusDataProtectionKeyCleanupOptions>(options =>
        {
            options.Enabled = true;
            configure?.Invoke(options);
        });

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, KyrolusDataProtectionKeyCleanupService>());

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionKeyRingRefreshHooks(
        this KyrolusDataProtectionBuilder builder,
        Action<KyrolusDataProtectionKeyRingRefreshOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<KyrolusDataProtectionKeyRingRefreshOptions>(options =>
        {
            options.Enabled = true;
            configure?.Invoke(options);
        });

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, KyrolusDataProtectionKeyRingRefreshService>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, KyrolusKeyRingRefreshNotifierListener>());

        return builder;
    }

    /// <summary>
    /// Enables automated background key rotation to provision a new key before the active key expires.
    /// </summary>
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionAutoKeyRotation(
        this KyrolusDataProtectionBuilder builder,
        Action<KyrolusDataProtectionKeyRotationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<KyrolusDataProtectionKeyRotationOptions>(options =>
        {
            options.EnableAutoRotation = true;
            configure?.Invoke(options);
        });

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, KyrolusKeyRotationWorker>());

        return builder;
    }
}
