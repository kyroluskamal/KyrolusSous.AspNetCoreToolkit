namespace KyrolusSous.Mapping.Abstractions.Contracts;

/// <summary>
/// Resolves a single destination member value during mapping operations, with support for Dependency Injection and custom state.
/// </summary>
/// <typeparam name="TSource">The source object type.</typeparam>
/// <typeparam name="TTarget">The destination object type.</typeparam>
/// <typeparam name="TMember">The destination property member type.</typeparam>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Generating full media CDN URLs by combining a relative path with an injected configuration setting:
/// <code>
/// public class ImageUrlResolver(IConfiguration config) : IKyrolusValueResolver&lt;Product, ProductDto, string&gt;
/// {
///     public string Resolve(Product source, ProductDto target, KyrolusMappingContext context)
///     {
///         return $"{config["Cdn:BaseUrl"]}/{source.ImagePath}";
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IKyrolusValueResolver<in TSource, in TTarget, out TMember>
{
    /// <summary>
    /// Computes the value for the destination property.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <param name="target">The target instance currently being constructed.</param>
    /// <param name="context">The mapping execution context.</param>
    /// <returns>The resolved destination member value.</returns>
    TMember Resolve(TSource source, TTarget target, KyrolusMappingContext context);
}
