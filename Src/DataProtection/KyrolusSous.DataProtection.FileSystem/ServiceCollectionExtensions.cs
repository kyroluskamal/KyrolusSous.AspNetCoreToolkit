using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.DataProtection.FileSystem;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionFileSystem(
        this KyrolusDataProtectionBuilder builder,
        string directoryPath,
        bool ensureDirectory = true)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }

        if (ensureDirectory)
        {
            Directory.CreateDirectory(directoryPath);
        }

        builder.DataProtection.PersistKeysToFileSystem(new DirectoryInfo(directoryPath));
        return builder;
    }
}
