namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Reacts to an exception without stopping it - log it, count it, alert on it. The exception is
/// still thrown afterwards.
/// </summary>
/// <remarks>
/// Think of it as a security camera: it records what happened, it does not prevent it. To
/// actually recover from the exception and return something instead, use
/// <see cref="IKyrolusRequestExceptionHandler{TRequest, TException, TResponse}"/>, which is given
/// a state object it can mark as handled. This interface has no such parameter, so it has no way
/// to swallow the exception even by accident.
/// <para>
/// <b>Every</b> matching action runs - unlike exception handlers, where the first one to recover
/// stops the rest. Logging, metrics and alerting should all fire, and none competes with another.
/// </para>
/// <para>
/// Matching walks the exception's inheritance chain from most specific outwards, so an action
/// registered for <see cref="Exception"/> catches everything, while one registered for a specific
/// type runs first.
/// </para>
/// <para>
/// <b>Wrap risky work in try/catch.</b> An exception thrown from here replaces the original one,
/// and the real failure is lost. A metrics backend being unreachable must not hide the bug that
/// actually broke the request.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <typeparam name="TException">
/// The exception type to react to. Use <see cref="Exception"/> to catch everything.
/// </typeparam>
/// <example>
/// <code>
/// public class RecordRateApiFailure(IMetrics metrics, ILogger&lt;RecordRateApiFailure&gt; logger)
///     : IKyrolusRequestExceptionAction&lt;GetExchangeRate, HttpRequestException&gt;
/// {
///     public Task Execute(GetExchangeRate request, HttpRequestException exception, CancellationToken ct)
///     {
///         try
///         {
///             metrics.Increment("exchange_rate_api_failures");
///             logger.LogError(exception, "Rate lookup failed {From}-&gt;{To}", request.From, request.To);
///         }
///         catch
///         {
///             // Never let a telemetry problem replace the real exception.
///         }
///
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusRequestExceptionAction<in TRequest, in TException>
    where TException : Exception
{
    /// <summary>Reacts to the exception. The exception is rethrown once every action has run.</summary>
    /// <param name="request">The request that failed.</param>
    /// <param name="exception">The exception the handler or a behavior threw.</param>
    /// <param name="cancellationToken">Signals that the caller gave up.</param>
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
