namespace KyrolusSous.Mediator.Runtime.GeneratorIntegration;

/// <summary>
/// Binds exception actions and exception handlers to their calls, without reflection.
/// </summary>
/// <remarks>
/// The exception behavior otherwise closes <c>IKyrolusRequestExceptionAction&lt;,&gt;</c> and
/// <c>IKyrolusRequestExceptionHandler&lt;,,&gt;</c> over the exception type with
/// <c>MakeGenericType</c>. The handler case is the one that cannot survive NativeAOT: its third
/// type argument is the response type, and a response of <c>int</c> needs an instantiation the
/// compiler emits only where it can see one.
/// <para>
/// A <see langword="null"/> result means the generator never saw that combination. That is the
/// normal case rather than an error: the behavior walks the exception's whole base chain, and
/// almost none of those types has an action registered for it.
/// </para>
/// </remarks>
public interface IKyrolusRequestExceptionDispatchSource
{
    /// <summary>
    /// The actions registered for one (request, exception) pair, already bound to their arguments.
    /// </summary>
    /// <remarks>
    /// Each entry carries the action's concrete type alongside the call. An action that throws is
    /// swallowed so it cannot replace the exception the request actually failed with, and the log
    /// entry that records it is useless without naming which action misbehaved.
    /// </remarks>
    IReadOnlyList<(Type ActionType, Func<CancellationToken, Task> Invoke)>? CreateActionInvocations(
        Type requestType,
        Type exceptionType,
        object request,
        Exception exception,
        IServiceProvider serviceProvider);

    /// <summary>
    /// The handlers registered for one (request, exception, response) triple.
    /// </summary>
    /// <param name="requestType">The request type the pipeline is closed over.</param>
    /// <param name="exceptionType">One type from the thrown exception's inheritance chain.</param>
    /// <param name="responseType">The response type the pipeline is closed over.</param>
    /// <param name="request">The request that failed.</param>
    /// <param name="exception">The exception the pipeline threw.</param>
    /// <param name="state">
    /// The <c>KyrolusRequestExceptionHandlerState&lt;TResponse&gt;</c> the handlers may mark as
    /// handled. Passed as <see cref="object"/> because the response type is not known here; the
    /// generated code casts it back to the type its own entry was written for.
    /// </param>
    /// <param name="serviceProvider">Resolves the registered handlers.</param>
    IReadOnlyList<Func<CancellationToken, Task>>? CreateHandlerInvocations(
        Type requestType,
        Type exceptionType,
        Type responseType,
        object request,
        Exception exception,
        object state,
        IServiceProvider serviceProvider);
}
