using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Validation.Abstractions;

public sealed record KyrolusValidationTraceContext(
    Type? RequestType,
    KyrolusValidationContext Context);

public interface IKyrolusValidationTracer
{
    object? Start(KyrolusValidationTraceContext context);

    ValueTask StopAsync(
        KyrolusValidationTraceContext context,
        object? state,
        IReadOnlyList<KyrolusValidationFailure> failures,
        Exception? exception = null,
        CancellationToken cancellationToken = default);
}
