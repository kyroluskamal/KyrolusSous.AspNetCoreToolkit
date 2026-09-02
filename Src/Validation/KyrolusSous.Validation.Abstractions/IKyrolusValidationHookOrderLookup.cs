namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Resolves a <see cref="KyrolusValidationHookOrderAttribute"/>-declared order for a hook's concrete type. The
/// engine consults this - when one is registered - before falling back to the hook's own
/// <see cref="IKyrolusValidationHook.Order"/> property, so the attribute (when present) wins without the hook
/// class needing to override the property itself.
/// </summary>
/// <remarks>
/// The only implementation shipped by this library is generated at compile time by
/// <c>KyrolusSous.Validation.Generator</c> and registered via its <c>AddKyrolusGeneratedValidationHookOrder()</c>.
/// A hand-written implementation works too, but at that point overriding <see cref="IKyrolusValidationHook.Order"/>
/// directly on the hook class is simpler.
/// </remarks>
public interface IKyrolusValidationHookOrderLookup
{
    /// <summary>
    /// Returns the declared order for <paramref name="hookType"/>, or <see langword="null"/> when no
    /// <see cref="KyrolusValidationHookOrderAttribute"/> was found for it - the caller should fall back to
    /// <see cref="IKyrolusValidationHook.Order"/> in that case, not treat <see langword="null"/> as <c>0</c>.
    /// </summary>
    /// <param name="hookType">The hook's concrete (runtime) type, i.e. <c>hook.GetType()</c>.</param>
    int? TryGetOrder(Type hookType);
}
