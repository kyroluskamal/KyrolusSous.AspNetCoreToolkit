using System.Xml.Linq;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionKeyRepository(
    IOptions<KeyManagementOptions> keyOptions)
    : IKyrolusDataProtectionKeyRepository
{
    private readonly IXmlRepository repository = ResolveRepository(keyOptions);

    public Task<IReadOnlyList<KyrolusDataProtectionKeyDocument>> ExportAsync(CancellationToken cancellationToken = default)
    {
        var elements = repository.GetAllElements();
        var documents = elements
            .Select(element =>
            {
                var name = element.Attribute("id")?.Value ?? "key";
                var xml = element.ToString(SaveOptions.DisableFormatting);
                return new KyrolusDataProtectionKeyDocument(name, xml);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<KyrolusDataProtectionKeyDocument>>(documents);
    }

    public Task ImportAsync(IEnumerable<KyrolusDataProtectionKeyDocument> documents, CancellationToken cancellationToken = default)
    {
        if (documents is null) throw new ArgumentNullException(nameof(documents));

        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.Xml))
            {
                continue;
            }

            var element = XElement.Parse(document.Xml);
            repository.StoreElement(element, document.FriendlyName);
        }

        return Task.CompletedTask;
    }

    private static IXmlRepository ResolveRepository(IOptions<KeyManagementOptions> options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var repository = options.Value.XmlRepository;
        if (repository is null)
        {
            throw new InvalidOperationException(
                "Data Protection XML repository is not configured. Configure a key persistence provider first.");
        }

        return repository;
    }
}
