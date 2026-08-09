using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Validation.Abstractions;

public sealed record KyrolusValidationMetricsContext(
    Type? RequestType,
    KyrolusValidationContext Context,
    IReadOnlyList<KyrolusValidationFailure> Failures,
    TimeSpan Duration);

public interface IKyrolusValidationMetrics
{
    ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default);
}
