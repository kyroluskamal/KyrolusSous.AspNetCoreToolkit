namespace KyrolusSous.Mapping.Abstractions.Contracts;

/// <summary>
/// Converts an entire source type <typeparamref name="TSource"/> into a destination type <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">The input source type.</typeparam>
/// <typeparam name="TTarget">The converted target type.</typeparam>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Converting between specialized domain value objects and raw primitive types (e.g., <c>Money</c> to <c>decimal</c>, or <c>string</c> to <c>Ulid</c>):
/// <code>
/// public class MoneyToDecimalConverter : IKyrolusTypeConverter&lt;Money, decimal&gt;
/// {
///     public decimal Convert(Money source, KyrolusMappingContext context) => source.Amount;
/// }
/// </code>
/// </para>
/// </remarks>
public interface IKyrolusTypeConverter<in TSource, out TTarget>
{
    /// <summary>
    /// Converts <paramref name="source"/> to <typeparamref name="TTarget"/>.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <param name="context">The mapping execution context.</param>
    /// <returns>The converted target instance.</returns>
    TTarget Convert(TSource source, KyrolusMappingContext context);
}
