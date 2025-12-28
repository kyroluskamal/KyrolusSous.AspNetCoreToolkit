namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenSoftDeletePolicy
{
    bool Enabled { get; }
    bool FilterDeletedByDefault { get; }
}
