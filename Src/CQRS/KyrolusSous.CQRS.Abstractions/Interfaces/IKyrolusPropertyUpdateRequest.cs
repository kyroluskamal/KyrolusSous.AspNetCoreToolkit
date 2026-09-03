namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Opt-in guard for a command that writes properties by name from a caller-supplied dictionary -
/// Patch, BulkPatch, and ExecuteUpdate, across both the EF and Marten CQRS providers. A command
/// whose <see cref="AllowedProperties"/> is non-null has every name in
/// <see cref="UpdatedPropertyNames"/> checked against it by
/// <c>KyrolusPropertyAllowListBehavior{TRequest, TResponse}</c> before the request reaches its
/// handler.
/// </summary>
/// <remarks>
/// Property names in these commands typically originate from a request body (a PATCH endpoint's
/// JSON, say) and are written via reflection with no column-level authorization of their own -
/// without this, any name that happens to match a public settable property on the entity can be
/// written, including ones an API was never meant to expose for editing (an IsAdmin flag, an
/// internal audit column). This is opt-in rather than mandatory because plenty of commands are
/// fully trusted (built entirely server-side, or already covered by field-level validation
/// elsewhere) and would gain nothing from listing every property they're allowed to touch.
/// </remarks>
public interface IKyrolusPropertyUpdateRequest
{
    /// <summary>Every property name this request would write if it reaches its handler.</summary>
    IEnumerable<string> UpdatedPropertyNames { get; }

    /// <summary>
    /// The only property names this request may write, or <see langword="null"/> to leave this
    /// request unrestricted (the default - opt in per command instance). Matched
    /// case-insensitively, the same way the EF/Marten repositories resolve a property name by
    /// reflection.
    /// </summary>
    IReadOnlySet<string>? AllowedProperties { get; }
}
