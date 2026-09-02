namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Declares a validation hook's <see cref="IKyrolusValidationHook.Order"/> without implementing the property in
/// source. Read at compile time by <c>KyrolusSous.Validation.Generator</c> - which emits a static
/// <c>Type -&gt; order</c> lookup registered as <see cref="IKyrolusValidationHookOrderLookup"/> - rather than by
/// runtime reflection, so it stays safe to use in a trimmed or Native AOT published app.
/// </summary>
/// <remarks>
/// Only takes effect once the compiling project references <c>KyrolusSous.Validation.Generator</c> and calls the
/// generated <c>AddKyrolusGeneratedValidationHookOrder()</c>. Without the generator wired up, a hook decorated
/// with this attribute silently falls back to <see cref="IKyrolusValidationHook.Order"/>'s default of <c>0</c> -
/// prefer overriding the <c>Order</c> property directly for a dependency-free alternative.
/// </remarks>
/// <example>
/// <code>
/// [KyrolusValidationHookOrder(1)]
/// public class KyrolusValidationMetricsHook : IKyrolusValidationHook { ... }
///
/// [KyrolusValidationHookOrder(2)]
/// public class KyrolusValidationTracingHook : IKyrolusValidationHook { ... }
/// </code>
/// </example>
/// <param name="order">The hook's <see cref="IKyrolusValidationHook.Order"/> - lower runs first.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class KyrolusValidationHookOrderAttribute(int order) : Attribute
{
    /// <summary>The hook's order. Lower values run first.</summary>
    public int Order { get; } = order;
}
