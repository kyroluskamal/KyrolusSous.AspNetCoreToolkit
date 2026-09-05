using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;

namespace KyrolusSous.CQRS.Validation;

/// <summary>
/// Pipeline behavior running per-item validation over a batch command's <c>Items</c> before its
/// handler executes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IKyrolusBatchCommand{TItem}"/>/<see cref="IKyrolusBatchCommand{TItem,TResponse}"/> had no
/// automatic validation of their own: <see cref="KyrolusValidationBehavior{TRequest,TResponse}"/>
/// validates the batch COMMAND itself (its own top-level properties), not the individual
/// <typeparamref name="TRequest"/>-typed items inside it, and <see cref="IKyrolusValidationEngine"/>'s
/// <c>ValidateBatchAsync</c> - purpose-built for exactly this ("validate N same-typed items, prefix
/// each failure's <c>FieldPath</c> with its index") - was never wired into the CQRS pipeline at all.
/// Without this behavior, a batch command's items only got validated if a handler remembered to call
/// the engine itself, inconsistently with how every other request is validated declaratively via the
/// pipeline.
/// </para>
/// <para>
/// <typeparamref name="TRequest"/>'s own item type is not statically known here (a batch command is
/// generic over its item type, not this behavior), so the closed <c>IKyrolusBatchCommand&lt;,&gt;</c>/
/// <c>IKyrolusBatchCommand&lt;&gt;</c> interface actually implemented by the concrete request type is
/// located via reflection, and the context-free, two-parameter overload of
/// <see cref="IKyrolusValidationEngine.ValidateBatchAsync{TRequest}(System.Collections.Generic.IEnumerable{TRequest}, System.Threading.CancellationToken)"/>
/// is invoked with that item type as its own generic argument - the same reflection-driven-generic-method
/// pattern already used elsewhere in this codebase (e.g. <c>KyrolusDefaultCacheKeyProvider</c>'s
/// response-type unwrapping) for a request type that is only known at runtime.
/// </para>
/// <para>
/// Ordered at -945, immediately adjacent to (one slot after) <see cref="KyrolusValidationBehavior{TRequest,TResponse}"/>'s
/// -950: both are validation/rejection concerns that must run before mass-assignment, telemetry,
/// idempotency, and throttling see the request, so a malformed item inside a batch is rejected before
/// consuming any of those - the same reasoning as <c>KyrolusValidationBehavior</c> itself.
/// </para>
/// <para>
/// Purely additive: a request that is not a batch command (<c>TryGetBatchItems</c> finds no matching
/// interface) passes straight through to <c>next()</c> untouched, and a batch command's items are only
/// checked when an <see cref="IKyrolusValidationEngine"/> is actually registered.
/// </para>
/// </remarks>
[PipelineOrder(-945)]
public sealed class KyrolusBatchValidationBehavior<TRequest, TResponse>(
    IKyrolusValidationEngine? engine = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    /// <summary>
    /// The context-free <c>ValidateBatchAsync&lt;TRequest&gt;(IEnumerable&lt;TRequest&gt;, CancellationToken)</c>
    /// open generic method definition - resolved once per closed <c>TRequest</c>/<c>TResponse</c> pair
    /// rather than re-resolved via <see cref="Type.GetMethods()"/> on every single <c>Handle</c> call.
    /// </summary>
    private static readonly MethodInfo ValidateBatchMethodDefinition = typeof(IKyrolusValidationEngine)
        .GetMethods()
        .Single(m => m.Name == nameof(IKyrolusValidationEngine.ValidateBatchAsync) && m.GetParameters().Length == 2);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        if (engine is not null && TryGetBatchItems(request, out var itemType, out var items))
        {
            var validateBatch = ValidateBatchMethodDefinition.MakeGenericMethod(itemType);
            var task = (ValueTask<IReadOnlyList<KyrolusValidationFailure>>)validateBatch.Invoke(engine, [items, cancellationToken])!;
            var failures = await task.ConfigureAwait(false);

            if (failures.Count > 0)
            {
                throw new KyrolusValidationException(failures);
            }
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reflects over <paramref name="request"/>'s interfaces looking for a closed
    /// <see cref="IKyrolusBatchCommand{TItem}"/> or <see cref="IKyrolusBatchCommand{TItem,TResponse}"/>,
    /// returning its item type and its <c>Items</c> collection (as a plain, non-generic
    /// <see cref="IEnumerable"/> suitable for a late-bound generic-method invocation) if found.
    /// </summary>
    private static bool TryGetBatchItems(TRequest request, [NotNullWhen(true)] out Type? itemType, [NotNullWhen(true)] out IEnumerable? items)
    {
        if (request is not null)
        {
            foreach (var iface in request.GetType().GetInterfaces())
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                var definition = iface.GetGenericTypeDefinition();
                if (definition != typeof(IKyrolusBatchCommand<>) && definition != typeof(IKyrolusBatchCommand<,>))
                {
                    continue;
                }

                var candidateItems = iface.GetProperty(nameof(IKyrolusBatchCommand<object>.Items))?.GetValue(request) as IEnumerable;
                if (candidateItems is not null)
                {
                    itemType = iface.GetGenericArguments()[0];
                    items = candidateItems;
                    return true;
                }
            }
        }

        itemType = null;
        items = null;
        return false;
    }
}
