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
    /// Registers a strongly-typed <see cref="IKyrolusLocalizer{TCategory}"/> backed by <see cref="KyrolusJsonLocalizer{TCategory}"/>,
    /// using the specified options. <see cref="KyrolusJsonLocalizationOptions.RequiredCategory"/> defaults to
    /// <c>typeof(TCategory).Name</c> (lowercased) before <paramref name="configure"/> runs, so it can still be
    /// overridden explicitly.
    /// </summary>
    public static IServiceCollection AddKyrolusJsonLocalization<TCategory>(
        this IServiceCollection services,
        Action<KyrolusJsonLocalizationOptions>? configure = null)
    {
        var options = new KyrolusJsonLocalizationOptions
        {
            RequiredCategory = typeof(TCategory).Name.ToLowerInvariant()
        };
        configure?.Invoke(options);

        services.TryAddSingleton<IKyrolusLocalizer<TCategory>>(_ => new KyrolusJsonLocalizer<TCategory>(options));
        return services;
    }

    /// <summary>
    /// Registers a strongly-typed <see cref="IKyrolusLocalizer{TCategory}"/> pointing to a directory and file pattern.
    /// </summary>
    public static IServiceCollection AddKyrolusJsonLocalization<TCategory>(
        this IServiceCollection services,
        string directoryPath,
        string filePattern = "*.json",
        string? requiredCategory = null)
    {
        return services.AddKyrolusJsonLocalization<TCategory>(opt =>
        {
            opt.DirectoryPath = directoryPath;
            opt.FilePattern = filePattern;
            if (requiredCategory is not null) opt.RequiredCategory = requiredCategory;
        });
    }

    /// <summary>
    /// Registers in-memory dictionary-based localization services.
    /// </summary>
    public static IServiceCollection AddKyrolusDictionaryLocalization(
        this IServiceCollection services,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> cultureMaps,
        IReadOnlyDictionary<string, string>? invariantMap = null,
        string? fallbackCulture = null,
        IEnumerable<string>? fallbackCultures = null)
    {
        services.TryAddSingleton<IKyrolusLocalizer>(new KyrolusDictionaryLocalizer(cultureMaps, invariantMap, fallbackCulture, fallbackCultures));
        return services;
    }
}
