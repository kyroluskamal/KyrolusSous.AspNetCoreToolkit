namespace KyrolusSous.CQRS.Mapping.Contracts;

/// <summary>
/// Allows a CQRS request to populate or configure contextual parameters into a <see cref="KyrolusMappingContext"/>
/// (e.g. current user ID, tenant ID, culture, timezone) during pipeline execution.
/// </summary>
public interface IKyrolusContextAwareMapping
{
    /// <summary>
    /// Configures or attaches contextual items into the specified <see cref="KyrolusMappingContext"/>.
    /// </summary>
    /// <param name="context">The mapping execution context.</param>
    void ConfigureMappingContext(KyrolusMappingContext context);
}
