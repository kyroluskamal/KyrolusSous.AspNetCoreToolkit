global using System.Data.Common;
global using System.Net;
global using KyrolusSous.ExceptionHandling.Abstractions.Helpers;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.ExceptionHandling.EntityFramework;

/// <summary>
/// Translates Entity Framework Core database exceptions (<see cref="DbUpdateConcurrencyException"/> and <see cref="DbUpdateException"/>)
/// into structured HTTP problem mappings.
/// </summary>
/// <remarks>
/// <para><b>Use Case 1 (Optimistic Concurrency Conflicts):</b></para>
/// When two users modify the same record concurrently, EF Core throws <see cref="DbUpdateConcurrencyException"/>.
/// This mapper converts it to HTTP 409 Conflict with code <c>"concurrency_conflict"</c> and marks it transient so the client can retry.
/// <para><b>Use Case 2 (Database Constraint/Update Failures):</b></para>
/// Converts <see cref="DbUpdateException"/> into HTTP 500 with code <c>"database_error"</c>, auto-detecting transient database timeouts.
/// </remarks>
/// <example>
/// <code>
/// // Registration in Program.cs:
/// builder.Services.AddKyrolusEntityFrameworkExceptionHandling();
/// </code>
/// </example>
public sealed class KyrolusEfExceptionMapper : IKyrolusExceptionMapper
{
    /// <summary>
    /// Gets the mapper order (-50 to execute before general framework mappers).
    /// </summary>
    public int Order => -50;

    /// <summary>
    /// Attempts to map EF Core exceptions into <see cref="KyrolusExceptionMapping"/>.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <param name="context">Ambient request context.</param>
    /// <param name="mapping">The mapped error result.</param>
    /// <returns><c>true</c> if mapped; otherwise, <c>false</c>.</returns>
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
