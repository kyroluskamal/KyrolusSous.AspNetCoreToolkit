using System.Net;
using KyrolusSous.ExceptionHandling.Abstractions.Helpers;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using Marten.Exceptions;
using Npgsql;

namespace KyrolusSous.ExceptionHandling.Marten;

/// <summary>
/// Maps Marten document database and Event Sourcing exceptions into standardized RFC 7807 problem mappings.
/// </summary>
/// <remarks>
/// <para><b>Use Case 1 (Event Sourcing Stream Concurrency):</b></para>
/// Maps <see cref="ConcurrentUpdateException"/> to HTTP 409 Conflict with transient retry flag.
/// <para><b>Use Case 2 (Stream Collision / Existing ID):</b></para>
/// Maps <see cref="ExistingStreamIdCollisionException"/> to HTTP 409 Conflict.
/// <para><b>Use Case 3 (Missing Event Stream):</b></para>
/// Maps <see cref="NonExistentStreamException"/> to HTTP 404 Not Found.
/// <para><b>Use Case 4 (PostgreSQL Unique Constraints):</b></para>
/// Translates <see cref="MartenCommandException"/> containing PostgreSQL unique violations (<c>23505</c>) to HTTP 409 Conflict.
/// </remarks>
/// <example>
/// <code>
/// // Registration in Program.cs:
/// builder.Services.AddKyrolusMartenExceptionHandling();
/// </code>
/// </example>
public sealed class KyrolusMartenExceptionMapper : IKyrolusExceptionMapper
{
    /// <summary>
    /// Gets the mapper order (-50 to execute before general fallback mappers).
    /// </summary>
    public int Order => -50;

    /// <summary>
    /// Attempts to map Marten document DB and Event Sourcing exceptions into <see cref="KyrolusExceptionMapping"/>.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <param name="context">Ambient request context.</param>
    /// <param name="mapping">The mapped error result.</param>
    /// <returns><c>true</c> if mapped; otherwise, <c>false</c>.</returns>
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
