using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.CustomXml;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionXmlRepository(
        this KyrolusDataProtectionBuilder builder,
        IXmlRepository repository)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (repository is null) throw new ArgumentNullException(nameof(repository));

        builder.DataProtection.AddKeyManagementOptions(o => o.XmlRepository = repository);
        return builder;
    }

    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionXmlRepository(
        this KyrolusDataProtectionBuilder builder,
        Func<IServiceProvider, IXmlRepository> factory)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new KyrolusXmlRepositoryOptionsSetup(factory(sp)));
        return builder;
    }

    private sealed class KyrolusXmlRepositoryOptionsSetup(IXmlRepository repository) : IConfigureOptions<KeyManagementOptions>
    {
        public void Configure(KeyManagementOptions options)
        {
            options.XmlRepository = repository;
        }
    }
}
