global using System.Data.Common;
global using System.Net;
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
            mapping = new KyrolusExceptionMapping(
                new KyrolusErrorEnvelope(KyrolusErrorCodes.ConcurrencyConflict, "Concurrency conflict", exception.Message, context.TraceId),
                HttpStatusCode.Conflict,
                IsTransient: false,
                ShouldLog: true);
            return true;
        }

        if (exception is DbUpdateException updateException)
        {
            var isTransient = updateException.InnerException is TimeoutException
                or TaskCanceledException
                or OperationCanceledException
                or DbException;

            mapping = new KyrolusExceptionMapping(
                new KyrolusErrorEnvelope(KyrolusErrorCodes.DatabaseError, "Database error", updateException.Message, context.TraceId),
                HttpStatusCode.InternalServerError,
                IsTransient: isTransient,
                ShouldLog: true);
            return true;
        }

        mapping = null!;
        return false;
    }
}
