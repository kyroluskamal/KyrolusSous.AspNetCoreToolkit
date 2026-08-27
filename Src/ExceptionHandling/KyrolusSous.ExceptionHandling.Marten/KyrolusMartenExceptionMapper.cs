using System.Net;
using KyrolusSous.ExceptionHandling.Abstractions.Helpers;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using Marten.Exceptions;
using Npgsql;

namespace KyrolusSous.ExceptionHandling.Marten;

/// <summary>
/// Maps Marten document database exceptions into standardized ProblemDetails / KyrolusExceptionMapping.
/// </summary>
public sealed class KyrolusMartenExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -50;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        var fullName = exception.GetType().FullName ?? string.Empty;

        if (exception is ConcurrentUpdateException ||
            fullName.Contains("ConcurrencyException", StringComparison.Ordinal) ||
            fullName.Contains("ConcurrentUpdateException", StringComparison.Ordinal))
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

        if (exception is ExistingStreamIdCollisionException ||
            fullName.Contains("CollisionException", StringComparison.Ordinal) ||
            fullName.Contains("DocumentAlreadyExists", StringComparison.Ordinal))
        {
            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.Conflict,
                title: "Resource conflict",
                statusCode: HttpStatusCode.Conflict,
                detail: exception.Message,
                traceId: context?.TraceId,
                errors: (exception as IKyrolusExceptionWithErrors)?.GetErrors(),
                metadata: KyrolusMetadataExtractor.Extract(exception));

            return true;
        }

        if (exception is NonExistentStreamException)
        {
            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.NotFound,
                title: "Stream not found",
                statusCode: HttpStatusCode.NotFound,
                detail: exception.Message,
                traceId: context?.TraceId,
                errors: (exception as IKyrolusExceptionWithErrors)?.GetErrors(),
                metadata: KyrolusMetadataExtractor.Extract(exception));

            return true;
        }

        if (exception is BadLinqExpressionException)
        {
            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.BadRequest,
                title: "Invalid query expression",
                statusCode: HttpStatusCode.BadRequest,
                detail: exception.Message,
                traceId: context?.TraceId,
                metadata: KyrolusMetadataExtractor.Extract(exception));

            return true;
        }

        if (exception is MartenCommandException commandException)
        {
            if (commandException.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                mapping = KyrolusExceptionMapping.Create(
                    code: KyrolusErrorCodes.Conflict,
                    title: "Unique constraint violation",
                    statusCode: HttpStatusCode.Conflict,
                    detail: pgEx.MessageText,
                    traceId: context?.TraceId,
                    metadata: KyrolusMetadataExtractor.Extract(commandException));

                return true;
            }

            var isTransient = commandException.InnerException is TimeoutException
                or TaskCanceledException
                or OperationCanceledException
                or NpgsqlException { IsTransient: true };

            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.DatabaseError,
                title: "Marten database error",
                statusCode: HttpStatusCode.InternalServerError,
                detail: commandException.Message,
                traceId: context?.TraceId,
                errors: (exception as IKyrolusExceptionWithErrors)?.GetErrors(),
                metadata: KyrolusMetadataExtractor.Extract(commandException))
                .AsTransient(isTransient);

            return true;
        }

        mapping = null!;
        return false;
    }
}
