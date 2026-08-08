using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using System.Globalization;
using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.CQRS.Marten.Command.Remove;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Repositories.Marten.Abstractions.Query;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunEndpointKitMartenRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = 0;
        checks += await RunDefaultCommandHandlerScenariosAsync(cancellationToken).ConfigureAwait(false);
        checks += await RunFilterBuilderScenariosAsync(cancellationToken).ConfigureAwait(false);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "endpointkit-marten-runtime",
            EndpointKitMartenChecks: checks,
            DbProbeCount: 0);
    }

    private static async Task<int> RunDefaultCommandHandlerScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        var removedCommands = new List<object>();
        var removeConfig = new KyrolusMartenApiConfig<MenuItem>
        {
            UseEnrichedCustomResponse = false,
            ViewModelType = typeof(MenuItem),
            RemoveRangeCommand = new RemoveRangeCommand<MenuItem>(Array.Empty<MenuItem>())
        };
        var removeHandler = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.Create(
            removeConfig,
            mediator: new RuntimeMediatorStub(command =>
            {
                removedCommands.Add(command);
                return null;
            }));
        var removeResult = await RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeHandleRemoveRangeAsync(
            removeHandler,
            [new MenuItem { Id = Guid.NewGuid(), Name = "remove-probe", Category = "Main", Price = 1 }],
            cacheable: false).ConfigureAwait(false);
        var removeResponse = await ExecuteResultAsync(removeResult).ConfigureAwait(false);
        if (removeResponse.StatusCode == StatusCodes.Status200OK
            && removedCommands.SingleOrDefault() is RemoveRangeCommand<MenuItem> removeCommand
            && removeCommand.Entities.Cast<MenuItem>().Single().Name == "remove-probe")
        {
            checks++;
        }

        var batchHandler = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEmptyPatchPayload, RuntimeEmptyPatchPayload, Guid>.Create(
            new KyrolusMartenApiConfig<RuntimeEmptyPatchPayload> { UseEnrichedCustomResponse = false });
        var batchResult = await RuntimeDefaultCommandQueryHandlerProbe<RuntimeEmptyPatchPayload, RuntimeEmptyPatchPayload, Guid>.ProbeExecuteBatchPatchAsync(
            batchHandler,
            new KyrolusBatchOperation<RuntimeEmptyPatchPayload, Guid>
            {
                OperationId = "no-updates",
                Operation = KyrolusBatchOperationType.Patch,
                Id = Guid.NewGuid(),
                Data = new RuntimeEmptyPatchPayload()
            },
            returnData: false,
            cancellationToken).ConfigureAwait(false);
        if (!batchResult.Success
            && batchResult.Status == StatusCodes.Status400BadRequest
            && string.Equals(batchResult.Error?.Code, "NO_UPDATES", StringComparison.Ordinal))
        {
            checks++;
        }

        var namedConcurrency = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeCreateNamedException("Marten.Exceptions.ConcurrentUpdateException");
        if (RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeIsConcurrencyException(namedConcurrency))
        {
            checks++;
        }

        var contextHandler = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.Create(
            new KyrolusMartenApiConfig<MenuItem> { UseEnrichedCustomResponse = false, ViewModelType = typeof(MenuItem) });
        var contextOk = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeTryEnsureContextMatch(
            contextHandler,
            new MenuItem { TenantId = "tenant-a", Name = "ctx", Category = "Main" },
            nameof(MenuItem.TenantId),
            "tenant-b",
            "tenant",
            out var contextError);
        var contextErrorResponse = await ExecuteResultAsync(contextError!).ConfigureAwait(false);
        if (!contextOk && contextErrorResponse.StatusCode == StatusCodes.Status404NotFound)
        {
            checks++;
        }

        var invalidOrderConfig = new KyrolusMartenApiConfig<MenuItem>
        {
            UseEnrichedCustomResponse = false,
            ViewModelType = typeof(MenuItem),
            StrictFilterValidation = true
        };
        var invalidOrderHandler = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.Create(invalidOrderConfig);
        var orderStringOk = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeTryBuildOrder(
            invalidOrderHandler,
            "Unknown:asc",
            out var orderStringError);
        var orderStringResponse = await ExecuteResultAsync(orderStringError!).ConfigureAwait(false);
        if (!orderStringOk
            && orderStringResponse.StatusCode == StatusCodes.Status400BadRequest
            && orderStringResponse.Body.Contains("Unknown", StringComparison.Ordinal))
        {
            checks++;
        }

        var orderClauseOk = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeTryBuildOrder(
            invalidOrderHandler,
            [new OrderClause("Unknown", false)],
            out var orderClauseError);
        var orderClauseResponse = await ExecuteResultAsync(orderClauseError!).ConfigureAwait(false);
        if (!orderClauseOk
            && orderClauseResponse.StatusCode == StatusCodes.Status400BadRequest
            && orderClauseResponse.Body.Contains("Unknown", StringComparison.Ordinal))
        {
            checks++;
        }

        var includeConfig = new KyrolusMartenApiConfig<RuntimeEndpointKitMartenProbeItem>
        {
            UseEnrichedCustomResponse = false,
            ViewModelType = typeof(RuntimeEndpointKitMartenProbeItem),
            MaxIncludeGraphDepth = 2,
            AllowedIncludeProperties = ["Child.Name", "Child.GrandChild.Label"]
        };
        var includeHandler = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.Create(includeConfig);
        var typedGraph = KyrolusIncludeGraphBuilder.FromPaths<RuntimeEndpointKitMartenProbeItem>("Child.Name");
        var typedGraphOk = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryBuildIncludeGraph(
            includeHandler,
            EndpointNames.Query,
            typedGraph,
            includeConfig.AllowedIncludeProperties,
            out var resolvedTypedGraph,
            out var typedGraphError);
        if (typedGraphOk && resolvedTypedGraph is not null && typedGraphError is null)
        {
            checks++;
        }

        var extractedList = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeExtractIncludeGraphPaths(
            new List<string> { " Child.Name ", " ", "Child.GrandChild.Label" });
        if (extractedList is ["Child.Name", "Child.GrandChild.Label"])
        {
            checks++;
        }

        var nonStrictIncludeOk = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryBuildIncludeGraph(
            includeHandler,
            EndpointNames.Query,
            new List<string> { "Child.Secret" },
            includeConfig.AllowedIncludeProperties,
            out var nonStrictGraph,
            out var nonStrictError);
        if (nonStrictIncludeOk && nonStrictGraph is null && nonStrictError is null)
        {
            checks++;
        }

        var strictIncludeHandler = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.Create(
            new KyrolusMartenApiConfig<RuntimeEndpointKitMartenProbeItem>
            {
                UseEnrichedCustomResponse = false,
                ViewModelType = typeof(RuntimeEndpointKitMartenProbeItem),
                MaxIncludeGraphDepth = 2,
                StrictIncludeValidation = true,
                AllowedIncludeProperties = ["Child.Name", "Child.GrandChild.Label"]
            });
        var strictAllowlistOk = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryBuildIncludeGraph(
            strictIncludeHandler,
            EndpointNames.Query,
            new List<string> { "Child.Secret" },
            ["Child.Name", "Child.GrandChild.Label"],
            out _,
            out var strictAllowlistError);
        var strictAllowlistResponse = await ExecuteResultAsync(strictAllowlistError!).ConfigureAwait(false);
        if (!strictAllowlistOk
            && strictAllowlistResponse.StatusCode == StatusCodes.Status400BadRequest
            && strictAllowlistResponse.Body.Contains("not allowed", StringComparison.OrdinalIgnoreCase))
        {
            checks++;
        }

        var strictMissingOk = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryBuildIncludeGraph(
            strictIncludeHandler,
            EndpointNames.Query,
            new List<string> { "Child.Missing" },
            null,
            out _,
            out var strictMissingError);
        var strictMissingResponse = await ExecuteResultAsync(strictMissingError!).ConfigureAwait(false);
        if (!strictMissingOk
            && strictMissingResponse.StatusCode == StatusCodes.Status400BadRequest
            && strictMissingResponse.Body.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            checks++;
        }

        var strictDepthOk = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryBuildIncludeGraph(
            strictIncludeHandler,
            EndpointNames.Query,
            new List<string> { "Child.GrandChild.Label" },
            null,
            out _,
            out var strictDepthError);
        var strictDepthResponse = await ExecuteResultAsync(strictDepthError!).ConfigureAwait(false);
        if (!strictDepthOk
            && strictDepthResponse.StatusCode == StatusCodes.Status400BadRequest
            && strictDepthResponse.Body.Contains("exceeds max depth", StringComparison.OrdinalIgnoreCase))
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeExtractIncludeGraphPaths(new { Name = "unsupported" }) is null)
        {
            checks++;
        }

        var etagHandler = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.Create(
            new KyrolusMartenApiConfig<MenuItem> { UseEnrichedCustomResponse = false, ViewModelType = typeof(MenuItem) });
        var rawBytes = new byte[] { 1, 2, 3, 4 };
        if (RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeNormalizeEtagValue(rawBytes) == Convert.ToBase64String(rawBytes))
        {
            checks++;
        }

        var etagGuid = Guid.NewGuid();
        if (RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeNormalizeEtagValue(etagGuid) == etagGuid.ToString("N"))
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeTryParseEtagValue(etagHandler, "\"text-etag\"", typeof(string), out var parsedString)
            && string.Equals(parsedString as string, "text-etag", StringComparison.Ordinal))
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeTryParseEtagValue(etagHandler, etagGuid.ToString("N"), typeof(Guid), out var parsedGuid)
            && parsedGuid is Guid parsedGuidValue
            && parsedGuidValue == etagGuid)
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeTryParseEtagValue(etagHandler, Convert.ToBase64String(rawBytes), typeof(byte[]), out var parsedBytes)
            && parsedBytes is byte[] parsedBytesValue
            && parsedBytesValue.SequenceEqual(rawBytes))
        {
            checks++;
        }

        var ifMatchContext = new DefaultHttpContext();
        ifMatchContext.Request.Headers.IfMatch = "\"probe-version\"";
        var ifMatchServices = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = ifMatchContext })
            .BuildServiceProvider();
        var readOnlyHandler = RuntimeDefaultCommandQueryHandlerProbe<RuntimeReadOnlyVersionProbeItem, RuntimeReadOnlyVersionProbeItem, Guid>.Create(
            new KyrolusMartenApiConfig<RuntimeReadOnlyVersionProbeItem>
            {
                UseEnrichedCustomResponse = false,
                ViewModelType = typeof(RuntimeReadOnlyVersionProbeItem),
                EnableEtags = true,
                RowVersionPropertyName = nameof(RuntimeReadOnlyVersionProbeItem.ReadOnlyVersion)
            },
            serviceProvider: ifMatchServices);
        var ifMatchOk = RuntimeDefaultCommandQueryHandlerProbe<RuntimeReadOnlyVersionProbeItem, RuntimeReadOnlyVersionProbeItem, Guid>.ProbeTryApplyIfMatch(
            readOnlyHandler,
            new RuntimeReadOnlyVersionProbeItem(),
            out var ifMatchError);
        var ifMatchResponse = await ExecuteResultAsync(ifMatchError!).ConfigureAwait(false);
        if (!ifMatchOk
            && ifMatchResponse.StatusCode == StatusCodes.Status400BadRequest
            && ifMatchResponse.Body.Contains("Invalid If-Match value.", StringComparison.Ordinal))
        {
            checks++;
        }

        var assignedHandler = RuntimeDefaultCommandQueryHandlerProbe<RuntimeAssignedIdProbeItem, RuntimeAssignedIdProbeItem, Guid>.Create(
            new KyrolusMartenApiConfig<RuntimeAssignedIdProbeItem>
            {
                UseEnrichedCustomResponse = false,
                ViewModelType = typeof(RuntimeAssignedIdProbeItem),
                SetEntityId = static (entity, id) => entity.AssignedId = (Guid)id!
            });
        var assigned = new RuntimeAssignedIdProbeItem();
        var assignedId = Guid.NewGuid();
        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeAssignedIdProbeItem, RuntimeAssignedIdProbeItem, Guid>.ProbeTrySetEntityId(
                assignedHandler,
                assigned,
                assignedId,
                out var assignedError)
            && assigned.AssignedId == assignedId
            && assignedError is null)
        {
            checks++;
        }

        var compositeHandler = RuntimeDefaultCommandQueryHandlerProbe<RuntimeAssignedIdProbeItem, RuntimeAssignedIdProbeItem, Guid>.Create(
            new KyrolusMartenApiConfig<RuntimeAssignedIdProbeItem>
            {
                UseEnrichedCustomResponse = false,
                ViewModelType = typeof(RuntimeAssignedIdProbeItem),
                CompositeKeyPropertyNames = []
            });
        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeAssignedIdProbeItem, RuntimeAssignedIdProbeItem, Guid>.ProbeTrySetCompositeKey(
                compositeHandler,
                new RuntimeAssignedIdProbeItem(),
                [Guid.NewGuid()],
                out var compositeError)
            && compositeError is null)
        {
            checks++;
        }

        var propertyProbe = new RuntimeEndpointKitMartenProbeItem { OptionalCount = 7 };
        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTrySetPropertyValue(
                propertyProbe,
                nameof(RuntimeEndpointKitMartenProbeItem.OptionalCount),
                null)
            && propertyProbe.OptionalCount is null)
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTrySetPropertyValue(
                propertyProbe,
                nameof(RuntimeEndpointKitMartenProbeItem.Amount),
                "12.5")
            && propertyProbe.Amount == 12.5m)
        {
            checks++;
        }

        var mergedAllowlist = RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeMergeAllowlist(
            ["Id", "Name"],
            ["name", "TenantId"]);
        if (mergedAllowlist is not null
            && mergedAllowlist.Count == 1
            && string.Equals(mergedAllowlist.Single(), "Name", StringComparison.OrdinalIgnoreCase))
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryConvertKey("raw-key", typeof(string), out var convertedString)
            && string.Equals(convertedString as string, "raw-key", StringComparison.Ordinal))
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryConvertKey("2024-03-01", typeof(DateOnly), out var convertedDate)
            && convertedDate is DateOnly dateOnly
            && dateOnly == new DateOnly(2024, 3, 1))
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryConvertKey("12:34:56", typeof(TimeOnly), out var convertedTime)
            && convertedTime is TimeOnly timeOnly
            && timeOnly == new TimeOnly(12, 34, 56))
        {
            checks++;
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryConvertKey("2024-03-01T01:02:03Z", typeof(DateTimeOffset), out var convertedDateTimeOffset))
        {
            var expectedDateTimeOffset = DateTimeOffset.Parse("2024-03-01T01:02:03Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (convertedDateTimeOffset is DateTimeOffset dto && dto == expectedDateTimeOffset)
            {
                checks++;
            }
        }

        if (RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTryConvertKey("2024-03-01T01:02:03Z", typeof(DateTime), out var convertedDateTime))
        {
            var expectedDateTime = DateTime.Parse("2024-03-01T01:02:03Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (convertedDateTime is DateTime dateTime && dateTime == expectedDateTime)
            {
                checks++;
            }
        }

        if (!RuntimeDefaultCommandQueryHandlerProbe<RuntimeEndpointKitMartenProbeItem, RuntimeEndpointKitMartenProbeItem, Guid>.ProbeTrySetPropertyValue(
                propertyProbe,
                nameof(RuntimeEndpointKitMartenProbeItem.Amount),
                null)
            && propertyProbe.Amount == 12.5m)
        {
            checks++;
        }

        var mappingConfig = new KyrolusMartenApiConfig<MenuItem>
        {
            UseEnrichedCustomResponse = false,
            ViewModelType = typeof(MenuItem),
            EndpointConfig =
            [
                new KyrolusEndpointConfig
                {
                    Name = EndpointNames.QueryPaged,
                    ViewModelType = typeof(RuntimeNameOnlyProjection)
                }
            ]
        };
        var mappingHandler = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.Create(mappingConfig);
        if (RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeResolveViewModelType(mappingHandler, EndpointNames.QueryPaged) == typeof(RuntimeNameOnlyProjection))
        {
            checks++;
        }

        var plainErrorHandler = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.Create(
            new KyrolusMartenApiConfig<MenuItem>
            {
                UseEnrichedCustomResponse = false,
                ViewModelType = typeof(MenuItem)
            });
        var plainConflict = RuntimeDefaultCommandQueryHandlerProbe<MenuItem, MenuItem, Guid>.ProbeBuildErrorResult(
            plainErrorHandler,
            StatusCodes.Status409Conflict,
            KyrolusErrorCodes.ConcurrencyConflict,
            "probe conflict");
        var plainConflictResponse = await ExecuteResultAsync(plainConflict).ConfigureAwait(false);
        if (plainConflictResponse.StatusCode == StatusCodes.Status409Conflict
            && plainConflictResponse.Body.Contains("probe conflict", StringComparison.Ordinal))
        {
            checks++;
        }

        return checks;
    }

    private static Task<int> RunFilterBuilderScenariosAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var checks = 0;

        var built = KyrolusSous.EndpointKit.Marten.FilterBuilder.BuildFilterExpression<MenuItem>("Name==Alpha");
        if (built is not null && built.Compile().Invoke(new MenuItem { Name = "Alpha" }))
        {
            checks++;
        }

        if (!TestQueryContractBridge.TryBuildClauseFilterExpression<MenuItem>(
                [new TestFilterClause(nameof(MenuItem.Price), "eq", "not-a-decimal")],
                null,
                strict: false,
                caseInsensitive: false,
                out _,
                out var conversionError)
            && conversionError?.Contains("Decimal", StringComparison.Ordinal) == true)
        {
            checks++;
        }

        if (!TestQueryContractBridge.TryBuildClauseFilterExpression<MenuItem>(
                [new TestFilterClause(nameof(MenuItem.Name), "has", "Alpha")],
                null,
                strict: false,
                caseInsensitive: false,
                out _,
                out var unsupportedError)
            && unsupportedError?.Contains("not supported", StringComparison.OrdinalIgnoreCase) == true)
        {
            checks++;
        }

        if (!TestQueryContractBridge.TryBuildClauseFilterExpression<MenuItem>(
                [new TestFilterClause(nameof(MenuItem.Price), "isnull", null)],
                null,
                strict: false,
                caseInsensitive: false,
                out _,
                out var nullOperatorError)
            && nullOperatorError?.Contains("does not allow null values", StringComparison.OrdinalIgnoreCase) == true)
        {
            checks++;
        }

        if (TestQueryContractBridge.TryBuildClauseFilterExpression<RuntimeNullableEnumProbeItem>(
                [new TestFilterClause(nameof(RuntimeNullableEnumProbeItem.Status), "in", "Active")],
                null,
                strict: false,
                caseInsensitive: false,
                out var nullableEnumFilter,
                out var nullableEnumError)
            && nullableEnumError is null
            && nullableEnumFilter is not null)
        {
            var compiled = nullableEnumFilter.Compile();
            if (compiled(new RuntimeNullableEnumProbeItem { Status = RuntimeSeekProbeStatus.Active })
                && !compiled(new RuntimeNullableEnumProbeItem { Status = RuntimeSeekProbeStatus.New })
                && !compiled(new RuntimeNullableEnumProbeItem { Status = null }))
            {
                checks++;
            }
        }

        var localDateTime = new DateTime(2024, 03, 02, 10, 11, 12, DateTimeKind.Local);
        if (RuntimeFilterBuilderProbe.ProbeTryConvert(localDateTime.ToString("o", CultureInfo.InvariantCulture), typeof(DateTime), out var localConverted)
            && localConverted is DateTime localDateTimeValue
            && localDateTimeValue.Kind == DateTimeKind.Utc)
        {
            checks++;
        }

        var unspecifiedDateTime = new DateTime(2024, 03, 02, 10, 11, 12, DateTimeKind.Unspecified);
        if (RuntimeFilterBuilderProbe.ProbeTryConvert(unspecifiedDateTime.ToString("o", CultureInfo.InvariantCulture), typeof(DateTime), out var unspecifiedConverted)
            && unspecifiedConverted is DateTime unspecifiedDateTimeValue
            && unspecifiedDateTimeValue.Kind == DateTimeKind.Utc)
        {
            checks++;
        }

        return Task.FromResult(checks);
    }

    private static Task<RuntimeResultPayload> ExecuteResultAsync(IResult? result)
    {
        if (result is null)
        {
            return Task.FromResult(new RuntimeResultPayload(StatusCodes.Status200OK, string.Empty));
        }

        var statusCode = (int?)(result.GetType().GetProperty("StatusCode")?.GetValue(result)) ?? StatusCodes.Status200OK;
        var value = result.GetType().GetProperty("Value")?.GetValue(result);
        var body = value switch
        {
            null => string.Empty,
            string text => text,
            _ => JsonSerializer.Serialize(value)
        };
        return Task.FromResult(new RuntimeResultPayload(statusCode, body));
    }

    private sealed record RuntimeResultPayload(int StatusCode, string Body);

    private sealed record FilterClausePayload(string Property, string Operator, string? Value);
}










