using KyrolusSous.DataProtection.Abstractions;
using Marten;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Marten;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionMarten(
        this KyrolusDataProtectionBuilder builder,
        IDocumentStore store,
        Action<KyrolusMartenKeyStorageOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(store);

        var options = new KyrolusMartenKeyStorageOptions();
        configureOptions?.Invoke(options);

        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IXmlRepository, KyrolusMartenXmlRepository>();
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new KyrolusMartenXmlRepositoryOptionsSetup(sp.GetRequiredService<IXmlRepository>()));

        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionMarten(
        this KyrolusDataProtectionBuilder builder,
        Action<StoreOptions> configureStore,
        Action<KyrolusMartenKeyStorageOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureStore);

        var store = DocumentStore.For(configureStore);
        return builder.AddKyrolusDataProtectionMarten(store, configureOptions);
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionMarten(
        this KyrolusDataProtectionBuilder builder,
        string connectionString,
        string? schemaName = null,
        Action<KyrolusMartenKeyStorageOptions>? configureOptions = null,
        Action<StoreOptions>? configureStore = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        var store = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                options.DatabaseSchemaName = schemaName;
            }

            configureStore?.Invoke(options);
        });

        return builder.AddKyrolusDataProtectionMarten(store, configureOptions);
    }

    private sealed class KyrolusMartenXmlRepositoryOptionsSetup(IXmlRepository repository)
        : IConfigureOptions<KeyManagementOptions>
    {
        public void Configure(KeyManagementOptions options)
        {
            options.XmlRepository = repository;
        }
    }
}
