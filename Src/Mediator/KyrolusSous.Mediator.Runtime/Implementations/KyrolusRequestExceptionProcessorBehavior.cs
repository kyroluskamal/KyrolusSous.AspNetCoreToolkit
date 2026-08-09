using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Mediator.Runtime.GeneratorIntegration;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Outermost behavior: catches anything thrown by the rest of the pipeline, runs the registered
/// exception actions, then gives the exception handlers a chance to supply a replacement response.
/// </summary>
/// <remarks>
/// Actions and handlers are bound by <see cref="IKyrolusRequestExceptionDispatchSource"/>. Doing it
/// here would mean closing <c>IKyrolusRequestExceptionHandler&lt;,,&gt;</c> over the response type
/// at runtime, and a response of <c>int</c> is exactly what an application published ahead of time
/// cannot close.
/// </remarks>
[PipelineOrder(-2000)]
public sealed class KyrolusRequestExceptionProcessorBehavior<TRequest, TResponse>(IServiceProvider serviceProvider)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    /// <summary>Stable logger category, rather than the mangled closed generic type name.</summary>
    private const string LoggerCategory = "KyrolusSous.Mediator.RequestExceptionProcessor";

    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IKyrolusRequestExceptionDispatchSource? _dispatchSource = serviceProvider.GetService<IKyrolusRequestExceptionDispatchSource>();

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ExecuteActionsAsync(request, ex, cancellationToken).ConfigureAwait(false);

            var state = new KyrolusRequestExceptionHandlerState<TResponse>();
            await ExecuteHandlersAsync(request, ex, state, cancellationToken).ConfigureAwait(false);
            if (state.Handled)
                return state.Response!;
            throw;
        }
    }

    /// <summary>
    /// Runs every registered action for the exception and each of its base types.
    /// </summary>
    /// <remarks>
    /// Each action is isolated. Actions are independent side effects - logging, metrics, alerting -
    /// so one of them failing must not skip the rest, and must never replace the exception the
    /// request actually failed with. An unreachable metrics backend hiding the real bug is a worse
    /// failure than the metric being missed.
    /// <para>
    /// A swallowed failure is still reported through <see cref="ILogger"/> when logging is
    /// registered, so this cannot hide a broken action forever.
    /// </para>
    /// </remarks>
    private async Task ExecuteActionsAsync(TRequest request, Exception exception, CancellationToken cancellationToken)
    {
        if (_dispatchSource is null) return;

        foreach (var exceptionType in GetExceptionTypes(exception))
        {
            // Null is the ordinary answer here: the loop walks the exception's whole base chain and
            // almost none of those types has an action registered for it.
            var invocations = _dispatchSource.CreateActionInvocations(
                typeof(TRequest), exceptionType, request!, exception, _serviceProvider);

            if (invocations is null) continue;

            foreach (var (actionType, invoke) in invocations)
            {
                try
                {
                    await invoke(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception actionFailure)
                {
                    ReportActionFailure(actionType, exception, actionFailure);
                }
            }
        }
    }

    private async Task ExecuteHandlersAsync(TRequest request,
        Exception exception,
        KyrolusRequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        if (_dispatchSource is null) return;

        foreach (var exceptionType in GetExceptionTypes(exception))
        {
            var invocations = _dispatchSource.CreateHandlerInvocations(
                typeof(TRequest), exceptionType, typeof(TResponse), request!, exception, state, _serviceProvider);

            if (invocations is null) continue;

            foreach (var invocation in invocations)
            {
                await invocation(cancellationToken).ConfigureAwait(false);

                // The first handler to recover wins: there is only one response to return, so a
                // second one would have nothing left to contribute.
                if (state.Handled) return;
            }
        }
    }

    /// <summary>
    /// Logs an action that threw, if logging is available. Deliberately best-effort: this runs
    /// while an exception is already in flight, so it must not throw a second one.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private void ReportActionFailure(Type actionType, Exception originalException, Exception actionFailure)
    {
        try
        {
            _serviceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger(LoggerCategory)
                .LogError(
                    actionFailure,
                    "[KyrolusMediator] Exception action {Action} failed while handling {OriginalException} from {Request}. The original exception is unaffected.",
                    actionType.FullName,
                    originalException.GetType().FullName,
                    typeof(TRequest).FullName);
        }
        catch
        {
            // Logging itself is broken. Nothing sensible is left to do, and throwing here would
            // destroy the very exception this behavior exists to preserve.
        }
    }

    /// <summary>
    /// Yields the exception's type and every base type up to <see cref="Exception"/>, most specific
    /// first - so a handler registered for a precise type runs before a general one.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private static IEnumerable<Type> GetExceptionTypes(Exception exception)
    {
        for (Type? current = exception.GetType();
             current is not null && typeof(Exception).IsAssignableFrom(current);
             current = current.BaseType)
            yield return current;
    }
}
