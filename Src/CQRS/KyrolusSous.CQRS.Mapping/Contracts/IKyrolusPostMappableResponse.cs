namespace KyrolusSous.CQRS.Mapping.Contracts;

/// <summary>
/// Marks a CQRS response that receives a post-processing mapping or enrichment pass via <see cref="IKyrolusObjectMapper"/>.
/// </summary>
public interface IKyrolusPostMappableResponse
{
    /// <summary>
    /// Executes post-processing or enrichment logic using the provided mapper and optional context.
    /// </summary>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">The optional mapping context.</param>
    void OnMapped(IKyrolusObjectMapper mapper, KyrolusMappingContext? context = null);
}
