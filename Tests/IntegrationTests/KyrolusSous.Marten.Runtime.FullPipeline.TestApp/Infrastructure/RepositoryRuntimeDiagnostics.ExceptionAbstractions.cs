using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Caching.Redis;
using KyrolusSous.DataProtection.Abstractions;
using KyrolusSous.DataProtection.Redis;
using KyrolusSous.DataProtection.Runtime;
using KyrolusSous.ExceptionHandling.Abstractions;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Redis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunExceptionAbstractionsRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var context = new KyrolusErrorContext(
            TraceId: "trace-1",
            CorrelationId: "correlation-1",
            UserId: "user-1",
            TenantId: "tenant-1",
            Path: "/api/diagnostics/exception",
            Method: "GET",
            Culture: CultureInfo.InvariantCulture);

        var code = $"diag_{Guid.NewGuid():N}";
        var definition = new KyrolusErrorCodeDefinition(code, "Diagnostics title", HttpStatusCode.Accepted, "Diagnostics description");
        KyrolusErrorCodeRegistry.Register(definition);
        Require(KyrolusErrorCodeRegistry.IsValidCode(code), "Exception abstractions runtime should validate registered error codes.", ref checks);
        Require(
            KyrolusErrorCodeRegistry.TryGet(code, out var fetchedDefinition) &&
            fetchedDefinition == definition,
            "Exception abstractions runtime should resolve registered error codes.",
            ref checks);
        Require(
            KyrolusErrorCodeRegistry.Snapshot().Any(item => item.Code == KyrolusErrorCodes.InternalError) &&
            KyrolusErrorCodeRegistry.Snapshot().Any(item => item.Code == code),
            "Exception abstractions runtime should expose registered error code snapshots.",
            ref checks);
        ExpectThrows<KyrolusErrorCodeRegistryException>(
            () => KyrolusErrorCodeRegistry.Register(definition),
            "Exception abstractions runtime should reject duplicate registrations.",
            ref checks);
        ExpectThrows<KyrolusErrorCodeRegistryException>(
            () => KyrolusErrorCodeRegistry.Register(new KyrolusErrorCodeDefinition("Invalid-Code", "Invalid", HttpStatusCode.BadRequest)),
            "Exception abstractions runtime should reject invalid code formats.",
            ref checks);
        ExpectThrows<KyrolusErrorCodeRegistryException>(
            () => KyrolusErrorCodeRegistry.Register(new KyrolusErrorCodeDefinition(" ", "Blank", HttpStatusCode.BadRequest)),
            "Exception abstractions runtime should reject blank code values.",
            ref checks);

        var rangeCode = $"diag_{Guid.NewGuid():N}";
        KyrolusErrorCodeRegistry.RegisterRange(
        [
            new KyrolusErrorCodeDefinition(rangeCode, "Range title", HttpStatusCode.Created)
        ]);
        Require(KyrolusErrorCodeRegistry.TryGet(rangeCode, out _), "Exception abstractions runtime should register ranges of codes.", ref checks);

        var validationErrors = new[] { new KyrolusErrorItem("Name", "name.required", "Name is required") };
        var notFound = new KyrolusNotFoundException("MenuItem", "42");
        var badRequest = new KyrolusBadRequestException("Bad request", "bad-detail");
        var conflict = new KyrolusConflictException("Conflict", "conflict-detail");
        var forbidden = new KyrolusForbiddenException("forbidden-detail");
        var unauthorized = new KyrolusUnauthorizedException("unauthorized-detail");
        var timeout = new KyrolusTimeoutException("timeout-detail");
        var rateLimit = new KyrolusRateLimitException("rate-limit-detail");
        var externalService = new KyrolusExternalServiceException("redis", "redis-detail");
        var validation = new KyrolusValidationException(validationErrors, "Validation title", "validation-detail");

        Require(notFound.EntityName == "MenuItem" && notFound.Key == "42" && notFound.StatusCode == HttpStatusCode.NotFound, "Exception abstractions runtime should preserve KyrolusNotFoundException metadata.", ref checks);
        Require(badRequest.Code == KyrolusErrorCodes.BadRequest && badRequest.Detail == "bad-detail", "Exception abstractions runtime should preserve bad-request metadata.", ref checks);
        Require(conflict.StatusCode == HttpStatusCode.Conflict && conflict.Detail == "conflict-detail", "Exception abstractions runtime should preserve conflict metadata.", ref checks);
        Require(forbidden.StatusCode == HttpStatusCode.Forbidden, "Exception abstractions runtime should preserve forbidden metadata.", ref checks);
        Require(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Exception abstractions runtime should preserve unauthorized metadata.", ref checks);
        Require(timeout.IsTransient && timeout.StatusCode == HttpStatusCode.GatewayTimeout, "Exception abstractions runtime should mark timeout exceptions as transient.", ref checks);
        Require(rateLimit.IsTransient && rateLimit.StatusCode == (HttpStatusCode)429, "Exception abstractions runtime should mark rate-limit exceptions as transient.", ref checks);
        Require(externalService.ServiceName == "redis" && externalService.IsTransient, "Exception abstractions runtime should preserve external service metadata.", ref checks);
        Require(validation.Errors?.Count == 1 && validation.Code == KyrolusErrorCodes.Validation, "Exception abstractions runtime should preserve validation failures.", ref checks);

        var envelope = new KyrolusErrorEnvelope(
            code,
            "Envelope title",
            "Envelope detail",
            context.TraceId,
            validationErrors,
            new Dictionary<string, object?> { ["tenant"] = context.TenantId });
        var mapping = new KyrolusExceptionMapping(envelope, HttpStatusCode.Conflict, IsTransient: true, ShouldLog: false);
        var result = new KyrolusErrorResult(envelope, HttpStatusCode.Conflict, IsTransient: true, ExceptionType: typeof(KyrolusConflictException).FullName);
        Require(mapping.IsTransient && !mapping.ShouldLog, "Exception abstractions runtime should preserve mapping metadata.", ref checks);
        Require(result.StatusCode == HttpStatusCode.Conflict && result.ExceptionType == typeof(KyrolusConflictException).FullName, "Exception abstractions runtime should preserve error results.", ref checks);

        var redisMapper = new KyrolusRedisExceptionMapper();
        Require(redisMapper.Order == -60, "Exception abstractions runtime should preserve Redis mapper ordering.", ref checks);
        Require(
            redisMapper.TryMap(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis down"), context, out var timeoutMapping) &&
            timeoutMapping.StatusCode == HttpStatusCode.GatewayTimeout &&
            timeoutMapping.Error.Code == KyrolusErrorCodes.Timeout,
            "Exception abstractions runtime should map Redis connection failures to timeout envelopes.",
            ref checks);
        Require(
            redisMapper.TryMap(new RedisServerException("redis server error"), context, out var externalMapping) &&
            externalMapping.StatusCode == HttpStatusCode.BadGateway &&
            externalMapping.Error.Code == KyrolusErrorCodes.ExternalService,
            "Exception abstractions runtime should map Redis server failures to external service envelopes.",
            ref checks);
        Require(
            !redisMapper.TryMap(new InvalidOperationException("not redis"), context, out _),
            "Exception abstractions runtime should decline unsupported exceptions.",
            ref checks);

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "exception-abstractions-runtime",
            ExceptionAbstractionsChecks: checks);
    }
}
