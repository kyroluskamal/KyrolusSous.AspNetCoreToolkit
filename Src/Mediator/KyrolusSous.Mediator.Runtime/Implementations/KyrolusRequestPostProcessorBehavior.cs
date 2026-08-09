namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Runs every registered <see cref="IKyrolusRequestPostProcessor{TRequest, TResponse}"/> after the
/// handler has produced a response.
/// </summary>
/// <remarks>
/// The mirror image of <see cref="KyrolusRequestPreProcessorBehavior{TRequest, TResponse}"/>: the
/// loop sits <em>after</em> <c>await next(...)</c>, so it runs on the way back out.
/// <para>
/// The placement is not a preference - it is forced. A post-processor is handed the response, and
/// the response does not exist until <c>next</c> has returned. Running the loop first would leave
/// nothing to pass it.
/// </para>
/// <para>
/// Post-processors can read the response but not replace it: <c>Process</c> returns a bare
/// <see cref="Task"/>, and this behavior returns the handler's response untouched. Use a plain
/// <see cref="IKyrolusPipelineBehavior{TRequest, TResponse}"/> to modify a response.
/// </para>
/// <para>
/// Ordered <c>+1000</c>, the innermost layer, so post-processors see the raw handler response
/// before any behavior you registered yourself gets a chance to wrap it.
/// </para>
/// <para>
/// Runs only on success. If the handler threw there is no response, so nothing here executes -
/// use <see cref="IKyrolusRequestExceptionAction{TRequest, TException}"/> for work that must
/// happen on failure too.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type flowing through the pipeline.</typeparam>
/// <typeparam name="TResponse">The response type flowing through the pipeline.</typeparam>
/// <example>
/// Given the same query and handler:
/// <code>
/// public record GetUser(Guid Id) : IKyrolusQuery&lt;User?&gt;;
///
/// public class GetUserHandler(AppDbContext db) : IKyrolusQueryHandler&lt;GetUser, User?&gt;
/// {
///     public async Task&lt;User?&gt; Handle(GetUser request, CancellationToken cancellationToken)
///         =&gt; await db.Users.FindAsync([request.Id], cancellationToken);
/// }
/// </code>
/// A post-processor records whether the lookup found anything - it needs the response, so it can
/// only run after the handler:
/// <code>
/// public class AuditUserLookups(IAuditLog audit) : IKyrolusRequestPostProcessor&lt;GetUser, User?&gt;
/// {
///     public Task Process(GetUser request, User? response, CancellationToken cancellationToken)
///         =&gt; audit.WriteAsync(response is null ? $"miss:{request.Id}" : $"hit:{request.Id}", cancellationToken);
/// }
/// </code>
/// With both processors registered, one request produces:
/// <code>
/// var user = await mediator.SendAsync(new GetUser(id));
/// // 1. LogUserLookups.Process     (pre  - order -1000)
/// // 2. GetUserHandler.Handle      (the handler)
/// // 3. AuditUserLookups.Process   (post - order +1000, gets the response)
/// </code>
/// </example>
[PipelineOrder(1000)]
public sealed class KyrolusRequestPostProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestPostProcessor<TRequest, TResponse>> postProcessors)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    // Materialised once: the collection is enumerated on every request, and GetServices may hand
    // back a lazy sequence that would re-resolve each time.
    private readonly IReadOnlyList<IKyrolusRequestPostProcessor<TRequest, TResponse>> _postProcessors =
        postProcessors as IReadOnlyList<IKyrolusRequestPostProcessor<TRequest, TResponse>> ?? [.. postProcessors];

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        foreach (var processor in _postProcessors)
            await processor.Process(request, response, cancellationToken).ConfigureAwait(false);

        return response;
    }
}
