using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.SoftDelete;

public sealed class KyrolusMartenNoSoftDeletePolicy : IKyrolusMartenSoftDeletePolicy
{
    public static readonly IKyrolusMartenSoftDeletePolicy Instance = new KyrolusMartenNoSoftDeletePolicy();

    public bool Enabled => false;
    public bool FilterDeletedByDefault => false;
    public string? PropertyName => null;
}

public sealed record KyrolusMartenSoftDeletePolicy(
    bool Enabled,
    bool FilterDeletedByDefault,
    string? PropertyName) : IKyrolusMartenSoftDeletePolicy
{
    public static KyrolusMartenSoftDeletePolicy For(
        string propertyName,
        bool filterDeletedByDefault = true,
        bool enabled = true)
        => new(enabled, filterDeletedByDefault, propertyName);

    public static KyrolusMartenSoftDeletePolicy IsDeleted(
        bool enabled = true,
        bool filterDeletedByDefault = true)
        => new(enabled, filterDeletedByDefault, "IsDeleted");
}
