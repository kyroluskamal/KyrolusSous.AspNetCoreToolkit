using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusFileKeyEscrowSink(string directoryPath) : IKeyEscrowSink
{
    private readonly string directoryPath = string.IsNullOrWhiteSpace(directoryPath)
        ? throw new ArgumentException("Directory path is required.", nameof(directoryPath))
        : directoryPath;

    public void Store(Guid keyId, XElement element)
    {
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, $"{keyId}.xml");
        File.WriteAllText(filePath, element.ToString(SaveOptions.DisableFormatting));
    }
}
