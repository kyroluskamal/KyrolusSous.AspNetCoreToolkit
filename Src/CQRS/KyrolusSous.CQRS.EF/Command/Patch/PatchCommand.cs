
namespace KyrolusSous.CQRS.EF.Command.Patch;

/// <remarks>
/// Declared <see cref="IKyrolusCommand{TResponse}"/> of <typeparamref name="TResponse"/>? - matching
/// <c>KyrolusSous.CQRS.Marten.Command.Patch.PatchCommand</c>'s <c>IKyrolusCommand&lt;TResponse?&gt;</c>
/// - because a patch targeting keys that do not match any row genuinely has nothing to return.
/// <see cref="PatchCommandHandler{TDbcontext, TResponse, TKey}"/> used to declare this non-nullable
/// and force-unwrap the repository's <c>TEntity?</c> result instead, silently handing callers a null
/// "non-null" value rather than one they were actually warned to check.
/// </remarks>
public class PatchCommand<TResponse, TKey>(object?[]? keyValues, Dictionary<string, object> updates, bool cacheable = false)
: CacheableRequest(cacheable), IKyrolusCommand<TResponse?>, IKyrolusPropertyUpdateRequest
    where TResponse : notnull
{
    public object?[]? KeyValues { get; set; } = keyValues;
    public Dictionary<string, object> Updates { get; set; } = updates;

    /// <inheritdoc cref="IKyrolusPropertyUpdateRequest.AllowedProperties"/>
    public IReadOnlySet<string>? AllowedProperties { get; set; }

    IEnumerable<string> IKyrolusPropertyUpdateRequest.UpdatedPropertyNames => Updates.Keys;
}
