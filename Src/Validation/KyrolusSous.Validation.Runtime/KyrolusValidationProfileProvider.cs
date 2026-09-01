namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// Default <see cref="IKyrolusValidationProfileProvider"/>: indexes every <see cref="KyrolusValidationProfile"/>
/// registered in DI (e.g. via <see cref="ServiceCollectionExtensions.AddKyrolusValidationProfile"/>) by name,
/// case-insensitively. A later-registered profile with the same name overwrites an earlier one.
/// </summary>
public sealed class KyrolusValidationProfileProvider : IKyrolusValidationProfileProvider
{
    private readonly Dictionary<string, KyrolusValidationContext> profiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Indexes every profile in <paramref name="profiles"/> by name; entries with a blank name are skipped.</summary>
    /// <param name="profiles">All <see cref="KyrolusValidationProfile"/> instances registered in the container.</param>
    public KyrolusValidationProfileProvider(IEnumerable<KyrolusValidationProfile> profiles)
    {
        foreach (var profile in profiles)
        {
            if (profile is null || string.IsNullOrWhiteSpace(profile.Name)) continue;

            this.profiles[profile.Name] = profile.Context;
        }
    }

    /// <inheritdoc />
    public bool TryGetProfile(string name, out KyrolusValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            context = KyrolusValidationContext.Default;
            return false;
        }

        if (profiles.TryGetValue(name, out var resolved))
        {
            context = resolved;
            return true;
        }

        context = KyrolusValidationContext.Default;
        return false;
    }
}
