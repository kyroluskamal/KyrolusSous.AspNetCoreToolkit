namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

/// <summary>
/// Defines the contract for an exception mapper that translates a specific CLR exception type into a structured <see cref="KyrolusExceptionMapping"/>.
/// </summary>
/// <remarks>
/// Implement this interface when creating custom translators for 3rd-party library exceptions (e.g. EF Core, Redis, Marten, Stripe).
/// Register it into DI via <c>services.AddSingleton&lt;IKyrolusExceptionMapper, MyCustomMapper&gt;()</c>.
/// </remarks>
/// <example>
/// <code>
/// public class PaymentExceptionMapper : IKyrolusExceptionMapper
/// {
///     public int Order => -10; // Lower numbers execute earlier
/// 
///     public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
///     {
///         if (exception is StripeException stripeEx)
///         {
///             mapping = KyrolusExceptionMapping.Create("payment_failed", "Payment Failed", HttpStatusCode.PaymentRequired, stripeEx.Message);
///             return true;
///         }
///         mapping = null!;
///         return false;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusExceptionMapper
{
    /// <summary>
    /// Gets the execution precedence order of this mapper. Lower numbers execute earlier in the mapping pipeline.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Attempts to translate the given exception into a structured <see cref="KyrolusExceptionMapping"/>.
    /// </summary>
    /// <param name="exception">The caught CLR exception.</param>
    /// <param name="context">The ambient request context information (e.g. TraceId, UserId).</param>
    /// <param name="mapping">When this method returns <c>true</c>, contains the mapped error definition.</param>
    /// <returns><c>true</c> if this mapper handles the exception; otherwise, <c>false</c>.</returns>
    bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping);
}
