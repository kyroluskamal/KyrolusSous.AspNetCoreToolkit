using KyrolusSous.DataProtection.Abstractions;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionKeyBackupService(
    IKyrolusDataProtectionKeyRepository repository)
{
    private readonly IKyrolusDataProtectionKeyRepository repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task ExportToDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }

        Directory.CreateDirectory(directoryPath);

        var documents = await repository.ExportAsync(cancellationToken).ConfigureAwait(false);
        foreach (var document in documents)
        {
            var safeName = SanitizeFileName(document.FriendlyName);
            var filePath = Path.Combine(directoryPath, $"{safeName}.xml");
            await File.WriteAllTextAsync(filePath, document.Xml, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ImportFromDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException(directoryPath);
        }

        var documents = new List<KyrolusDataProtectionKeyDocument>();
        foreach (var path in Directory.EnumerateFiles(directoryPath, "*.xml"))
        {
            var xml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            documents.Add(new KyrolusDataProtectionKeyDocument(
                FriendlyName: Path.GetFileNameWithoutExtension(path),
                Xml: xml));
        }

        await repository.ImportAsync(documents, cancellationToken).ConfigureAwait(false);
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "key";

        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
