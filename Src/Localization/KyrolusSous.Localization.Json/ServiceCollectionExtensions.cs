using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KyrolusSous.Localization.Abstractions;

namespace KyrolusSous.Localization.Json;

/// <summary>
/// Service collection extension methods for registering Kyrolus Localization services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers JSON-based localization services using the specified options.
    /// </summary>
    public static IServiceCollection AddKyrolusJsonLocalization(
        this IServiceCollection services,
        Action<KyrolusJsonLocalizationOptions>? configure = null)
    {
        var options = new KyrolusJsonLocalizationOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IKyrolusLocalizer>(_ => new KyrolusJsonLocalizer(options));
        return services;
    }

    /// <summary>
    /// Registers JSON-based localization services pointing to a directory and file pattern.
    /// </summary>
    public static IServiceCollection AddKyrolusJsonLocalization(
        this IServiceCollection services,
        string directoryPath,
        string filePattern = "*.json",
        string? requiredCategory = null)
    {
        return services.AddKyrolusJsonLocalization(opt =>
        {
            opt.DirectoryPath = directoryPath;
            opt.FilePattern = filePattern;
            opt.RequiredCategory = requiredCategory;
        });
    }

    /// <summary>
    /// Registers in-memory dictionary-based localization services.
    /// </summary>
    public static IServiceCollection AddKyrolusDictionaryLocalization(
        this IServiceCollection services,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> cultureMaps,
        IReadOnlyDictionary<string, string>? invariantMap = null)
    {
        services.TryAddSingleton<IKyrolusLocalizer>(new KyrolusDictionaryLocalizer(cultureMaps, invariantMap));
        return services;
    }
}
