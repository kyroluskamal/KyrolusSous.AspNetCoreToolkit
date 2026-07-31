namespace KyrolusSous.Mediator.Abstractions.Interfaces;
/// <summary>
/// Runs before the handler. Use it for work that happens on the side and does not change what the
/// request does - logging it, stamping a correlation id, recording a metric.
/// </summary>
/// <remarks>
/// This is the simple half of <see cref="IKyrolusPipelineBehavior{TRequest, TResponse}"/>. A
/// behavior receives a <c>next</c> delegate and so can skip the handler, catch its exceptions, or
/// replace its response; a pre-processor gets none of that, which is the point - there is no
/// <c>next</c> to forget to call.
/// <para>
/// It can still stop the request, but only by throwing. That is the right tool for a guard clause
/// and the wrong one for control flow.
/// </para>
/// <para>
/// Every registered pre-processor runs, in registration order, before the handler is reached.
/// They are collected by <c>KyrolusRequestPreProcessorBehavior</c>, itself a behavior at order
/// -1000, so they sit outside your own behaviors but inside exception handling.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">
/// The request type this runs for. Contravariant (<c>in</c>): consumed, never returned. Leave it
/// open (<c>MyProcessor&lt;TRequest&gt;</c>) to run for every request.
/// </typeparam>
/// <example>
/// <code>
/// public class LogUserLookups(ILogger&lt;LogUserLookups&gt; logger) : IKyrolusRequestPreProcessor&lt;GetUser&gt;
/// {
///     public Task Process(GetUser request, CancellationToken cancellationToken)
///     {
///         logger.LogInformation("Looking up {Id}", request.Id);
///         return Task.CompletedTask;
///     }
/// }
///
/// public class LogEverything&lt;TRequest&gt;(ILogger&lt;TRequest&gt; logger) : IKyrolusRequestPreProcessor&lt;TRequest&gt;
/// {
///     public Task Process(TRequest request, CancellationToken cancellationToken)
///     {
///         logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusRequestPreProcessor<in TRequest>
{
    /// <summary>Runs before the handler.</summary>
    /// <param name="request">The request about to be handled.</param>
    /// <param name="cancellationToken">Signals that the caller gave up.</param>
    /// <returns>A task that completes when this pre-processor is done. Throwing aborts the request.</returns>
    Task Process(TRequest request, CancellationToken cancellationToken);
}
