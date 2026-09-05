using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KyrolusSous.CQRS.Abstractions.Security;

namespace KyrolusSous.CQRS.EF.Command.Bulk;

/// <remarks>
/// Always-on, independent of <c>IKyrolusPropertyUpdateRequest.AllowedProperties</c> (which is opt-in
/// and does nothing when a caller never sets it): a key, concurrency-token, or database-generated
/// column must never be writable through any EF command handler that accepts an arbitrary
/// <c>Dictionary&lt;string, object&gt;</c> of property names to update - <c>ExecuteUpdateCommandHandler</c>,
/// <c>PatchCommandHandler</c>, and <c>BulkPatchCommandHandler</c> all take this same input shape.
/// Extracted from <c>ExecuteUpdateCommandHandler</c>, which used to be the only one of the three
/// enforcing this.
/// </remarks>
internal static class EfProtectedPropertyGuard
{
    public static bool IsProtectedFromUpdate(PropertyInfo prop)
    {
        if (prop.IsDefined(typeof(KeyAttribute), inherit: true)) return true;
        if (prop.IsDefined(typeof(TimestampAttribute), inherit: true)) return true;
        if (prop.IsDefined(typeof(ConcurrencyCheckAttribute), inherit: true)) return true;
        var generated = prop.GetCustomAttribute<DatabaseGeneratedAttribute>(inherit: true);
        return generated is { DatabaseGeneratedOption: DatabaseGeneratedOption.Computed };
    }

    /// <summary>
    /// Throws <see cref="KyrolusSecurityException"/> naming <paramref name="operationName"/> when
    /// <paramref name="prop"/> is protected; a no-op otherwise.
    /// </summary>
    public static void ThrowIfProtected(PropertyInfo prop, Type entityType, string operationName)
    {
        if (!IsProtectedFromUpdate(prop)) return;
        throw new KyrolusSecurityException(
            $"[Kyrolus CQRS Security] Property '{prop.Name}' on '{entityType.Name}' is a key, " +
            $"concurrency-token, or database-generated column and cannot be updated via {operationName}.");
    }
}
