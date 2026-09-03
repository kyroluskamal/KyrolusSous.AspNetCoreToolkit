using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Optional pipeline behavior that logs the start, completion and failure of every request
/// through the standard <see cref="Microsoft.Extensions.Logging"/> API - mirroring how
/// <see cref="KyrolusMediatorMetricsBehavior{TRequest, TResponse}"/> does the same for metrics.
/// Not registered by default.
/// </summary>
/// <remarks>
/// Logs under the category <c>"KyrolusSous.Mediator.Requests"</c>: an entry at
/// <see cref="LogLevel.Debug"/> when a request starts, <see cref="LogLevel.Information"/> when it
/// completes, and <see cref="LogLevel.Warning"/> (with the exception attached) when it faults.
/// <para>
/// Only the request type's simple name and the elapsed time are logged - never the request or
/// response object itself - so this cannot leak the content of a message into logs by accident.
/// Write your own behavior instead if you need specific fields logged.
/// </para>
/// <para>
/// <see cref="ILoggerFactory"/> is resolved optionally: an application with no logging configured
/// still works, it just gets no log output from this behavior.
/// </para>
/// <para>
/// Registered like any other open-generic behavior; when published ahead of time,
/// <c>KyrolusSous.Mediator.Generator</c> closes it at compile time for every (request, response)
/// pair it can see, the same way it closes any other <c>AddOpenBehavior</c> registration.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddKyrolusMediator(configuration =&gt;
/// {
///     configuration.RegisterServicesFromAssemblyContaining&lt;Program&gt;();
///     configuration.AddKyrolusMediatorLogging();
/// });
/// </code>
/// </example>
public sealed class KyrolusMediatorLoggingBehavior<TRequest, TResponse>(ILoggerFactory? loggerFactory = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private const string LoggerCategory = "KyrolusSous.Mediator.Requests";

    private readonly ILogger? _logger = loggerFactory?.CreateLogger(LoggerCategory);

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_logger is null)
            return await next(cancellationToken).ConfigureAwait(false);

        var requestName = typeof(TRequest).Name;
        var startTimestamp = Stopwatch.GetTimestamp();

        _logger.LogDebug("[KyrolusMediator] {RequestType} starting.", requestName);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "[KyrolusMediator] {RequestType} completed in {ElapsedMilliseconds}ms.",
                requestName,
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "[KyrolusMediator] {RequestType} failed after {ElapsedMilliseconds}ms.",
                requestName,
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            throw;
        }
    }
}
