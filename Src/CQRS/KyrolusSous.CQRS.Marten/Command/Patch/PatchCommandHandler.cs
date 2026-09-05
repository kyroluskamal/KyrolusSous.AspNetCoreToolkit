using KyrolusSous.CQRS.Marten.Command.Bulk;

namespace KyrolusSous.CQRS.Marten.Command.Patch;

public class PatchCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<PatchCommand<TResponse, TKey>, TResponse?>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse?> Handle(PatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        // Always-on, independent of IKyrolusPropertyUpdateRequest.AllowedProperties (which is opt-in
        // and does nothing when a caller never sets it): the document's identity and Marten's own
        // concurrency/revision tracking must never be writable through Patch regardless of allow-list
        // configuration. Mirrors ExecuteUpdateCommandHandler's guard for the same
        // Dictionary<string, object> input shape - see MartenProtectedPropertyGuard.
        foreach (var name in command.Updates.Keys)
        {
            var prop = typeof(TResponse).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is not null)
            {
                MartenProtectedPropertyGuard.ThrowIfProtected(prop, typeof(TResponse), "Patch");
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var result = await repo.PatchAsync(command.Id, command.Updates, command.TenantId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (result?.Entity is null) return null;
        if (!string.IsNullOrWhiteSpace(command.RowVersionPropertyName) && result.Version.HasValue)
        {
            TrySetRowVersion(result.Entity, command.RowVersionPropertyName, result.Version.Value);
        }
        return result.Entity;
    }

    private static void TrySetRowVersion(TResponse entity, string propertyName, Guid version)
    {
        var prop = typeof(TResponse).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null || !prop.CanWrite) return;

        if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
        {
            prop.SetValue(entity, version);
            return;
        }

        if (prop.PropertyType == typeof(string))
        {
            prop.SetValue(entity, version.ToString("N"));
            return;
        }

        if (prop.PropertyType == typeof(byte[]))
        {
            prop.SetValue(entity, version.ToByteArray());
        }
    }
}
