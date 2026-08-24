using KyrolusSous.Repositories.Marten.Abstractions.Metadata;

namespace KyrolusSous.Repositories.Marten.Runtime.Metadata;

/// <summary>
/// Default in-memory or ambient accessor for Marten event stream metadata.
/// </summary>
public sealed class KyrolusMartenDefaultEventMetadataProvider(Func<KyrolusMartenEventMetadataContext>? contextAccessor = null) : IKyrolusMartenEventMetadataProvider
{
    public IReadOnlyDictionary<string, object> GetMetadata()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var ctx = contextAccessor?.Invoke();

        if (ctx is not null)
        {
            if (!string.IsNullOrEmpty(ctx.CorrelationId))
                dict["correlation-id"] = ctx.CorrelationId;

            if (!string.IsNullOrEmpty(ctx.CausationId))
                dict["causation-id"] = ctx.CausationId;

            if (!string.IsNullOrEmpty(ctx.UserId))
                dict["user-id"] = ctx.UserId;

            if (!string.IsNullOrEmpty(ctx.TenantId))
                dict["tenant-id"] = ctx.TenantId;
        }

        dict["timestamp-utc"] = DateTime.UtcNow;
        return dict;
    }
}
