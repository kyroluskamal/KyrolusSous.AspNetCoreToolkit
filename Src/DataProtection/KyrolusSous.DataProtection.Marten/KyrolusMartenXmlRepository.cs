using System.Xml.Linq;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace KyrolusSous.DataProtection.Marten;

public sealed class KyrolusMartenXmlRepository(
    IDocumentStore store,
    KyrolusMartenKeyStorageOptions options)
    : IXmlRepository
{
    private readonly IDocumentStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly KyrolusMartenKeyStorageOptions options = options ?? throw new ArgumentNullException(nameof(options));

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var session = OpenSession();
        var documents = session.Query<KyrolusMartenDataProtectionKey>().ToList();
        if (documents.Count == 0)
        {
            return [];
        }

        return [.. documents
            .Where(document => !string.IsNullOrWhiteSpace(document.Xml))
            .Select(document => XElement.Parse(document.Xml, LoadOptions.PreserveWhitespace))];
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));

        var id = element.Attribute("id")?.Value ?? Guid.NewGuid().ToString("N");
        var name = string.IsNullOrWhiteSpace(friendlyName) ? "key" : friendlyName;
        var xml = element.ToString(SaveOptions.DisableFormatting);

        using var session = OpenSession();
        session.Store(new KyrolusMartenDataProtectionKey
        {
            Id = id,
            FriendlyName = name,
            Xml = xml,
            CreatedAt = DateTimeOffset.UtcNow
        });

        session.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private IDocumentSession OpenSession()
    {
        if (options.UseLightweightSession)
        {
            return string.IsNullOrWhiteSpace(options.TenantId)
                ? store.LightweightSession()
                : store.LightweightSession(options.TenantId);
        }

        var sessionOptions = new SessionOptions();
        if (!string.IsNullOrWhiteSpace(options.TenantId))
        {
            sessionOptions.TenantId = options.TenantId;
        }

        return store.OpenSession(sessionOptions);
    }

    private sealed class KyrolusMartenDataProtectionKey
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FriendlyName { get; set; } = string.Empty;
        public string Xml { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
