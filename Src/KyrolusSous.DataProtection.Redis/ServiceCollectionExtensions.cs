using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KyrolusSous.DataProtection.Redis;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionRedis(
        this KyrolusDataProtectionBuilder builder,
        IConnectionMultiplexer connection,
        string key = "DataProtection-Keys")
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));

        builder.Services.TryAddSingleton(connection);
        builder.DataProtection.PersistKeysToStackExchangeRedis(connection, key);
        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionRedis(
        this KyrolusDataProtectionBuilder builder,
        string connectionString,
        string key = "DataProtection-Keys")
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        var connection = ConnectionMultiplexer.Connect(connectionString);
        builder.Services.TryAddSingleton<IConnectionMultiplexer>(connection);
        builder.DataProtection.PersistKeysToStackExchangeRedis(connection, key);
        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionRedisKeyRingRefreshNotifications(
        this KyrolusDataProtectionBuilder builder,
        Action<KyrolusRedisKeyRingRefreshOptions>? configure = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddSingleton<KyrolusRedisKeyRingRefreshNotifier>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IKyrolusKeyRingRefreshNotifier>(
            sp => sp.GetRequiredService<KyrolusRedisKeyRingRefreshNotifier>()));

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<KyrolusDataProtectionKeyRingRefreshOptions>>(
                new KyrolusKeyRingRefreshOptionsConfigurator()));

        return builder;
    }

    private sealed class KyrolusKeyRingRefreshOptionsConfigurator
        : IConfigureOptions<KyrolusDataProtectionKeyRingRefreshOptions>
    {
        public void Configure(KyrolusDataProtectionKeyRingRefreshOptions options)
        {
            options.EnableCrossInstanceNotifications = true;
            options.RefreshOnExternalSignal = true;
            options.PublishLocalChanges = true;
        }
    }
}
