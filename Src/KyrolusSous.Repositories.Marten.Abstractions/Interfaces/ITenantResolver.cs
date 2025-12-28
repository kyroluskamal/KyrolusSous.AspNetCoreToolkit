namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface ITenantResolver
{
    string? ResolveTenantId();
}
