using KyrolusSous.CQRS.Abstractions.Security;

namespace KyrolusSous.CQRS.Marten.Command.Bulk;

/// <remarks>
/// Marten's own analogs of EF's key/concurrency-token guard: a property literally named <c>Id</c> is
/// always the document's primary key by Marten's baseline convention, no attribute required.
/// <see cref="JasperFx.IdentityAttribute"/> is Marten's opt-in way to designate a <em>different</em>
/// property as the document identity when it isn't named <c>Id</c>. And a property named
/// <c>Version</c> is Marten's own optimistic-concurrency/revision tracker - but only when the entity
/// type actually opts into one of Marten's version-tracking marker interfaces
/// (<see cref="JasperFx.Metadata.IVersioned"/> for the Guid-based ETag, <see cref="JasperFx.IRevisioned"/>
/// for the 32-bit revision, <see cref="JasperFx.ILongVersioned"/> for the 64-bit one) - a document that
/// never implements any of them has no such property to protect and Marten attaches no special meaning
/// to a same-named field there.
///
/// Always-on, independent of <c>IKyrolusPropertyUpdateRequest.AllowedProperties</c> (which is opt-in
/// and does nothing when a caller never sets it): this applies to every Marten command handler that
/// accepts an arbitrary <c>Dictionary&lt;string, object&gt;</c> of property names to update -
/// <c>ExecuteUpdateCommandHandler</c>, <c>PatchCommandHandler</c>, and <c>BulkPatchCommandHandler</c>
/// all take this same input shape. Extracted from <c>ExecuteUpdateCommandHandler</c>, which used to be
/// the only one of the three enforcing this.
/// </remarks>
internal static class MartenProtectedPropertyGuard
{
    public static bool IsProtectedFromUpdate(PropertyInfo prop, Type entityType)
    {
        if (string.Equals(prop.Name, "Id", StringComparison.Ordinal)) return true;
        if (prop.IsDefined(typeof(JasperFx.IdentityAttribute), inherit: true)) return true;

        if (string.Equals(prop.Name, "Version", StringComparison.Ordinal) &&
            (typeof(JasperFx.Metadata.IVersioned).IsAssignableFrom(entityType) ||
             typeof(JasperFx.IRevisioned).IsAssignableFrom(entityType) ||
             typeof(JasperFx.ILongVersioned).IsAssignableFrom(entityType)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Throws <see cref="KyrolusSecurityException"/> naming <paramref name="operationName"/> when
    /// <paramref name="prop"/> is protected; a no-op otherwise.
    /// </summary>
    public static void ThrowIfProtected(PropertyInfo prop, Type entityType, string operationName)
    {
        if (!IsProtectedFromUpdate(prop, entityType)) return;
        throw new KyrolusSecurityException(
            $"[Kyrolus CQRS Security] Property '{prop.Name}' on '{entityType.Name}' is a Marten " +
            $"document identity or version/revision property and cannot be updated via {operationName}.");
    }
}
