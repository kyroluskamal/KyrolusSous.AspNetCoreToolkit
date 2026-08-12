namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationProfileProvider : IKyrolusValidationProfileProvider
{
    private readonly Dictionary<string, KyrolusValidationContext> profiles = new(StringComparer.OrdinalIgnoreCase);

    public KyrolusValidationProfileProvider(IEnumerable<KyrolusValidationProfile> profiles)
    {
        foreach (var profile in profiles)
        {
            if (profile is null || string.IsNullOrWhiteSpace(profile.Name)) continue;

            this.profiles[profile.Name] = profile.Context;
        }
    }

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
