namespace KyrolusSous.Mapping.Abstractions.Contracts;

/// <summary>
/// Defines an explicit, executable mapping implementation contract for custom, hand-coded conversion logic.
/// </summary>
/// <typeparam name="TSource">The origin source type.</typeparam>
/// <typeparam name="TTarget">The destination target type.</typeparam>
/// <remarks>
/// <para>
/// <b>Difference between <see cref="IKyrolusMapTo{TTarget}"/>, <see cref="IKyrolusMapFrom{TSource}"/>, and <see cref="IKyrolusMapWith{TSource, TTarget}"/>:</b>
/// <list type="bullet">
///   <item>
///     <description>
///       <b><see cref="IKyrolusMapTo{TTarget}"/> &amp; <see cref="IKyrolusMapFrom{TSource}"/>:</b>
///       Marker interfaces (no methods to implement). They signal to the generator and runtime to perform <b>automatic property-matching</b>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b><see cref="IKyrolusMapWith{TSource, TTarget}"/>:</b>
///       An <b>executable implementation contract</b> containing <see cref="Map(TSource, KyrolusMappingContext)"/>.
///       Used when mapping cannot be done automatically by matching property names (e.g. legacy data decoding, binary parsing, or complex domain rules).
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Real-World Use Cases:</b>
/// <list type="number">
///   <item>
///     <description>
///       <b>Custom Dedicated Mapper Class:</b>
///       <code>
///       public class LegacyCustomerMapper : IKyrolusMapWith&lt;LegacyCustomerRow, CustomerDto&gt;
///       {
///           public CustomerDto Map(LegacyCustomerRow source, KyrolusMappingContext context)
///           {
///               // Custom decoding logic:
///               return new CustomerDto
///               {
///                   FullName = $"{source.LastName}, {source.FirstName}",
///                   DecodedFlags = Convert.ToString(source.RawBitmask, 2)
///               };
///           }
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Self-Mapping DTO (Encapsulated Custom Logic inside the DTO itself):</b>
///       <code>
///       public class OrderReportDto : IKyrolusMapWith&lt;Order, OrderReportDto&gt;
///       {
///           public string Summary { get; set; } = string.Empty;
///           
///           public OrderReportDto Map(Order source, KyrolusMappingContext context)
///           {
///               return new OrderReportDto { Summary = $"Order #{source.Id} Total: ${source.Total}" };
///           }
///       }
///       </code>
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
public interface IKyrolusMapWith<in TSource, out TTarget>
{
    /// <summary>
    /// Executes custom, hand-coded mapping logic from <paramref name="source"/> to <typeparamref name="TTarget"/>.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <param name="context">The mapping execution context.</param>
    /// <returns>The mapped destination instance.</returns>
    TTarget Map(TSource source, KyrolusMappingContext context);
}
