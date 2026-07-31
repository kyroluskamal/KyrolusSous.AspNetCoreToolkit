namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Runs every registered <see cref="IKyrolusRequestPreProcessor{TRequest}"/> before the handler.
/// </summary>
/// <remarks>
/// This is the engine, not something you write. You write pre-processors; the library registers
/// this one behavior to find and run them. Nothing else in the pipeline can do it, because only a
/// behavior receives <c>next</c> - the delegate that continues towards the handler - and running
/// "before the handler" means running before that delegate is called.
/// <para>
/// The loop sits <em>before</em> <c>await next(...)</c>, which is what makes it "pre". Code placed
/// after that call runs on the way back out instead - that is
/// <see cref="KyrolusRequestPostProcessorBehavior{TRequest, TResponse}"/>.
/// </para>
/// <para>
/// Ordered <c>-1000</c>, so pre-processors run before any behavior you register yourself
/// (which default to order 0), but still inside exception handling at <c>-2000</c>.
/// </para>
/// <para>
/// Processors are <b>not</b> isolated from each other: one throwing aborts the request and the
/// rest are skipped. That is intended - a pre-processor is normally a guard, and a guard that
/// fails should stop the request.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type flowing through the pipeline.</typeparam>
/// <typeparam name="TResponse">The response type flowing through the pipeline.</typeparam>
/// <example>
/// Given a query and its handler:
/// <code>
/// public record GetUser(Guid Id) : IKyrolusQuery&lt;User?&gt;;
///
/// public class GetUserHandler(AppDbContext db) : IKyrolusQueryHandler&lt;GetUser, User?&gt;
/// {
///     public async Task&lt;User?&gt; Handle(GetUser request, CancellationToken cancellationToken)
///         =&gt; await db.Users.FindAsync([request.Id], cancellationToken);
/// }
/// </code>
/// A pre-processor logs every lookup without the handler knowing it exists:
/// <code>
/// public class LogUserLookups(ILogger&lt;LogUserLookups&gt; logger) : IKyrolusRequestPreProcessor&lt;GetUser&gt;
/// {
///     public Task Process(GetUser request, CancellationToken cancellationToken)
///     {
///         logger.LogInformation("Looking up {Id}", request.Id);
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// Assembly scanning picks it up, so sending the query prints the line and then runs the handler:
/// <code>
/// builder.Services.AddKyrolusMediatorFromAssemblies(typeof(Program).Assembly);
///
/// var user = await mediator.SendAsync(new GetUser(id));
/// // "Looking up 3f2a..."   &lt;- the pre-processor
/// // then GetUserHandler.Handle runs
/// </code>
/// </example>
[PipelineOrder(-1000)]
public sealed class KyrolusRequestPreProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestPreProcessor<TRequest>> preProcessors)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    // Materialised once: the collection is enumerated on every request, and GetServices may hand
    // back a lazy sequence that would re-resolve each time.
    private readonly IReadOnlyList<IKyrolusRequestPreProcessor<TRequest>> _preProcessors =
        preProcessors as IReadOnlyList<IKyrolusRequestPreProcessor<TRequest>> ?? [.. preProcessors];

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        foreach (var processor in _preProcessors)
            await processor.Process(request, cancellationToken).ConfigureAwait(false);

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
