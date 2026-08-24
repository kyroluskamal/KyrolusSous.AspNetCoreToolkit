global using System.Data.Common;
global using System.Net;
global using KyrolusSous.ExceptionHandling.Abstractions.Helpers;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.ExceptionHandling.EntityFramework;

public sealed class KyrolusEfExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -50;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.ConcurrencyConflict,
                title: "Concurrency conflict",
                statusCode: HttpStatusCode.Conflict,
                detail: exception.Message,
                traceId: context?.TraceId,
                errors: (exception as IKyrolusExceptionWithErrors)?.GetErrors(),
                metadata: KyrolusMetadataExtractor.Extract(exception))
                .AsTransient();

            return true;
        }

        if (exception is DbUpdateException updateException)
        {
            var isTransient = updateException.InnerException is TimeoutException
                or TaskCanceledException
                or OperationCanceledException
                or DbException;

            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.DatabaseError,
                title: "Database error",
                statusCode: HttpStatusCode.InternalServerError,
                detail: updateException.Message,
                traceId: context?.TraceId,
                errors: (exception as IKyrolusExceptionWithErrors)?.GetErrors(),
                metadata: KyrolusMetadataExtractor.Extract(updateException))
                .AsTransient(isTransient);

            return true;
        }

        mapping = null!;
        return false;
    }
}
