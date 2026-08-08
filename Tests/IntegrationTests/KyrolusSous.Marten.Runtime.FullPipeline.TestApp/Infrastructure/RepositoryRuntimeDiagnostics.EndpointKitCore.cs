using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Marten.Command.Add;
using KyrolusSous.CQRS.Marten.Command.Patch;
using KyrolusSous.CQRS.Marten.Command.Remove;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Core.Envelope;
using KyrolusSous.EndpointKit.Core.FieldSelection;
using KyrolusSous.EndpointKit.Core.Hateoas;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.ExceptionHandling;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.ClasesAndHelpers;
using KyrolusSous.ExceptionHandling.FluentValidation;
using KyrolusSous.ExceptionHandling.Handlers;
using KyrolusSous.ExceptionHandling.Interfaces;
using KyrolusSous.ExceptionHandling.Mapping;
using KyrolusSous.ExceptionHandling.Writers;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Repositories.Marten.Abstractions.Authorization;
using KyrolusSous.Repositories.Marten.Abstractions;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Observer;
using KyrolusSous.Repositories.Marten.Abstractions.Query;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using KyrolusSous.Repositories.Marten.Abstractions.Resilience;
using KyrolusSous.Repositories.Marten.Abstractions.SoftDelete;
using KyrolusSous.Repositories.Marten.Abstractions.Specifications;
using KyrolusSous.Repositories.Marten.Abstractions.Tracing;
using KyrolusSous.Repositories.Marten.Abstractions.Validation;
using KyrolusSous.Repositories.Marten.Runtime;
using KyrolusSous.Repositories.Marten.Runtime.EventStore;
using KyrolusSous.Repositories.Marten.Runtime.Projection;
using KyrolusSous.Repositories.Marten.Runtime.Repository;
using KyrolusSous.Repositories.Marten.Runtime.Repository.Decorators;
using KyrolusSous.Repositories.Marten.Runtime.Saga;
using KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;
using KyrolusSous.Validation.Abstractions;
using KyrolusSous.Validation.FluentValidation;
using KyrolusSous.Validation.Runtime;
using KyrolusSous.CQRS.Validation;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Marten.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Npgsql;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    private static async Task<int> RunFieldSelectionScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        if (KyrolusFieldSelectionParser.TryParse(null, out var selectAll, out var selectAllError) &&
            selectAll.SelectAll &&
            selectAllError is null)
        {
            checks++;
        }

        if (KyrolusFieldSelectionParser.TryParse(
            "Id,CustomerName,Category[Id,Name],Lines[Product,Quantity],LineArray[Product],ReadOnlyLines[Quantity]",
            out var nestedSelection,
            out var nestedError) &&
            nestedError is null &&
            nestedSelection.IsFieldSelected("Category") &&
            nestedSelection.GetNestedSelection("Category")?.IsFieldSelected("Name") == true)
        {
            checks++;
        }

        if (!KyrolusFieldSelectionParser.TryParse("Id,Category[Id,Name", out _, out var parserError) &&
            !string.IsNullOrWhiteSpace(parserError))
        {
            checks++;
        }

        var parsedPaths = KyrolusFieldSelectionParser.Parse(new[] { "Category.Name", "Category.Id", "Lines.Product", " ", "LineArray.Quantity" });
        if (!parsedPaths.SelectAll && parsedPaths.GetNestedSelection("Category") is not null)
        {
            checks++;
        }

        if (KyrolusFieldSelectionParser.TryParse("  Category.Name  ,  CustomerName  ", out var dottedSelection, out var dottedError) &&
            dottedError is null &&
            dottedSelection.GetNestedSelection("Category")?.IsFieldSelected("Name") == true &&
            dottedSelection.IsFieldSelected("CustomerName"))
        {
            checks++;
        }

        var order = new RuntimeFieldSelectionOrder
        {
            Id = Guid.NewGuid(),
            CustomerName = "Field-Selection",
            Category = new RuntimeFieldSelectionCategory { Id = 7, Name = "Hot" },
            Lines =
            [
                new RuntimeFieldSelectionLine { Product = "Coffee", Quantity = 2 },
                new RuntimeFieldSelectionLine { Product = "Cake", Quantity = 1 }
            ],
            LineArray =
            [
                new RuntimeFieldSelectionLine { Product = "Tea", Quantity = 3 }
            ],
            ReadOnlyLines =
            [
                new RuntimeFieldSelectionLine { Product = "Water", Quantity = 4 }
            ],
            CustomEnumerableLines = new RuntimeFieldSelectionLineBag(
            [
                new RuntimeFieldSelectionLine { Product = "Juice", Quantity = 5 }
            ])
        };

        var projectedSingle = KyrolusFieldProjector.ProjectSingle(order, parsedPaths);
        if (projectedSingle.ContainsKey(nameof(RuntimeFieldSelectionOrder.Category)))
        {
            checks++;
        }

        var projectedCollection = KyrolusFieldProjector.ProjectCollection(new[] { order }, parsedPaths);
        if (projectedCollection.Count == 1)
        {
            checks++;
        }

        var projectedAny = KyrolusFieldProjector.Project(order, parsedPaths);
        if (projectedAny is Dictionary<string, object?> projectedMap &&
            projectedMap.ContainsKey(nameof(RuntimeFieldSelectionOrder.CustomerName)))
        {
            checks++;
        }

        var projectedList = KyrolusFieldProjector.Project(new[] { order }, parsedPaths);
        if (projectedList is IReadOnlyList<Dictionary<string, object?>> projectedListMap &&
            projectedListMap.Count == 1)
        {
            checks++;
        }

        if (KyrolusFieldProjector.Project(null, parsedPaths) is null)
        {
            checks++;
        }

        var projectedPage = KyrolusFieldProjector.ProjectPaged(
            new List<RuntimeFieldSelectionOrder> { order },
            totalCount: 1,
            pageNumber: 1,
            pageSize: 10,
            selection: parsedPaths);
        if (projectedPage.TotalPages == 1 && projectedPage.HasNextPage == false && projectedPage.HasPreviousPage == false)
        {
            checks++;
        }

        var projectedPageWithZeroSize = KyrolusFieldProjector.ProjectPaged(
            new List<RuntimeFieldSelectionOrder> { order },
            totalCount: 1,
            pageNumber: 1,
            pageSize: 0,
            selection: parsedPaths);
        if (projectedPageWithZeroSize.TotalPages == 0)
        {
            checks++;
        }

        if (KyrolusFieldValidator.Validate<RuntimeFieldSelectionOrder>(selectAll, out var selectAllInvalidFields) &&
            selectAllInvalidFields.Count == 0)
        {
            checks++;
        }

        KyrolusFieldValidator.Validate(typeof(RuntimeFieldSelectionOrder), parsedPaths, "", out var validInvalidFields);
        if (validInvalidFields.Count == 0)
        {
            checks++;
        }

        var invalidSelection = KyrolusFieldSelectionParser.Parse(new[] { "MissingField", "Category.MissingNested", "Lines.MissingNested" });
        var isValid = KyrolusFieldValidator.Validate(typeof(RuntimeFieldSelectionOrder), invalidSelection, "", out var invalidFields);
        if (!isValid &&
            invalidFields.Contains("MissingField") &&
            invalidFields.Any(x => x.Contains("MissingNested", StringComparison.OrdinalIgnoreCase)))
        {
            checks++;
        }

        var customEnumerableSelection = KyrolusFieldSelectionParser.Parse(["CustomEnumerableLines.Quantity"]);
        if (KyrolusFieldValidator.Validate<RuntimeFieldSelectionOrder>(customEnumerableSelection, out var customEnumerableInvalidFields) &&
            customEnumerableInvalidFields.Count == 0)
        {
            checks++;
        }

        await Task.Yield();
        return checks;
    }

    private static async Task<int> RunEnvelopeScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;
        var options = new KyrolusEnvelopeOptions
        {
            IncludeMeta = true,
            IncludeTimestamp = true,
            IncludeTraceId = true,
            IncludeVersion = true,
            IncludePagination = true,
            Hateoas = new KyrolusHateoasOptions
            {
                Enabled = true
            }
        };

        var builder = new KyrolusEnvelopeBuilder(options)
            .WithData(new { Name = "Envelope" })
            .WithStatusCode(StatusCodes.Status202Accepted)
            .WithTraceId("trace-1")
            .WithVersion("v1")
            .WithPagination(totalCount: 12, page: 2, pageSize: 5)
            .WithLinks([KyrolusLink.Self("/api/runtime")]);

        var successEnvelope = builder.Build();
        if (successEnvelope.Success &&
            successEnvelope.Meta?.TotalPages == 3 &&
            successEnvelope.Meta?.HasMore == true &&
            successEnvelope.Links?.Count == 1)
        {
            checks++;
        }

        var errorEnvelope = new KyrolusEnvelopeBuilder(options)
            .WithStatusCode(StatusCodes.Status400BadRequest)
            .WithError("bad_request", "Invalid request", [new KyrolusErrorDetail("name", "required", "Name is required")])
            .Build();
        if (!errorEnvelope.Success &&
            errorEnvelope.Error?.Code == "bad_request" &&
            errorEnvelope.Error.Details?.Count == 1)
        {
            checks++;
        }

        var ctorOk = new KyrolusResponseEnvelope(new { Value = 1 }, new KyrolusResponseMeta { Status = 200 });
        if (ctorOk.Success && ctorOk.Meta?.Status == 200)
        {
            checks++;
        }

        var ctorFail = new KyrolusResponseEnvelope("conflict", "Already exists", null);
        if (!ctorFail.Success && ctorFail.Error?.Code == "conflict")
        {
            checks++;
        }

        var staticOk = KyrolusResponseEnvelope.Ok(new { Value = 2 });
        var staticFail = KyrolusResponseEnvelope.Fail("not_found", "Missing");
        if (staticOk.Success && !staticFail.Success)
        {
            checks++;
        }

        var noMetaOptions = new KyrolusEnvelopeOptions { IncludeMeta = false };
        var noMetaEnvelope = new KyrolusEnvelopeBuilder(noMetaOptions)
            .WithData(new { Value = 3 })
            .Build();
        if (noMetaEnvelope.Meta is null)
        {
            checks++;
        }

        await Task.Yield();
        return checks;
    }

    private static async Task<int> RunHateoasScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("diag.local");
        httpContext.Request.PathBase = new PathString("/gateway");
        httpContext.Request.QueryString = new QueryString("?q=spicy&pageNumber=9&pageSize=50");

        var generator = new KyrolusDefaultLinkGenerator(new RuntimeNoopLinkGenerator());

        var configAll = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            Prefix = "api",
            Route = "menu-items",
            ApiVersion = "1",
            AppendVersionToPrefix = true,
            Endpoints = [EndpointNames.All]
        };

        var itemLinks = generator.GenerateItemLinks(httpContext, configAll, Guid.Empty, new RuntimeLinkItem { Id = Guid.Empty, Name = "X" });
        if (itemLinks.Any(x => x.Rel == KyrolusLinkRel.Self) &&
            itemLinks.Any(x => x.Rel == KyrolusLinkRel.Edit) &&
            itemLinks.Any(x => x.Rel == KyrolusLinkRel.Delete))
        {
            checks++;
        }

        var collectionLinks = generator.GenerateCollectionLinks(
            httpContext,
            configAll,
            pageNumber: 2,
            pageSize: 10,
            totalCount: 35);
        if (collectionLinks.Any(x => x.Rel == KyrolusLinkRel.Self) &&
            collectionLinks.Any(x => x.Rel == KyrolusLinkRel.Create) &&
            collectionLinks.Any(x => x.Rel == KyrolusLinkRel.Next))
        {
            checks++;
        }

        var configRestricted = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            Prefix = "api",
            Route = "menu-items",
            ApiVersion = null,
            AppendVersionToPrefix = false,
            Endpoints = [EndpointNames.GetAll],
            AllEndpointsExcept = [EndpointNames.Delete]
        };

        var restrictedItemLinks = generator.GenerateItemLinks(httpContext, configRestricted, 10, new RuntimeLinkItem { Id = Guid.NewGuid(), Name = "Y" });
        if (!restrictedItemLinks.Any(x => x.Rel == KyrolusLinkRel.Edit) &&
            !restrictedItemLinks.Any(x => x.Rel == KyrolusLinkRel.Delete))
        {
            checks++;
        }

        var pagedLinks = generator.GeneratePagedLinks(httpContext, configAll, pageNumber: 1, pageSize: 5, totalCount: 0);
        if (pagedLinks.Any(x => x.Rel == KyrolusLinkRel.Self) &&
            pagedLinks.Any(x => x.Rel == KyrolusLinkRel.First) &&
            !pagedLinks.Any(x => x.Rel == KyrolusLinkRel.Last))
        {
            checks++;
        }

        var relatedLink = KyrolusLink.Related("runtime-related", "/api/runtime/related", "Related runtime item");
        var customLink = new KyrolusLink("runtime-custom", "/api/runtime/custom", "PATCH", "Patch runtime item", "application/json");
        if (relatedLink.Rel == "runtime-related" &&
            relatedLink.Href == "/api/runtime/related" &&
            relatedLink.Method == "GET" &&
            relatedLink.Title == "Related runtime item" &&
            customLink.Type == "application/json")
        {
            checks++;
        }

        await Task.Yield();
        return checks;
    }

    private static async Task<int> RunOpenApiSchemaProviderScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;
        var provider = new KyrolusDefaultOpenApiSchemaProvider();
        var config = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeLinkItem",
            Prefix = "api",
            Route = "runtime-link-item"
        };

        foreach (var endpoint in Enum.GetValues<EndpointNames>())
        {
            var description = provider.GetDescription(config, endpoint);
            var summary = provider.GetSummary(config, endpoint);

            if (endpoint is EndpointNames.All or EndpointNames.Custom)
            {
                if (description is null && summary is null)
                {
                    checks++;
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(description) || !string.IsNullOrWhiteSpace(summary))
            {
                checks++;
            }
        }

        var tags = provider.GetTags(config, EndpointNames.GetAll);
        var operationId = provider.GetOperationId(config, EndpointNames.GetAll);
        if (tags is null && operationId is null)
        {
            checks++;
        }

        await Task.Yield();
        return checks;
    }

    private static async Task<int> RunOpenApiMetadataScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;
        using var app = WebApplication.CreateBuilder().Build();
        var group = app.MapGroup("/api/openapi-runtime");

        var endpointResponseConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeLinkItem",
            Prefix = "api",
            Route = "runtime-link-item",
            EndpointConfig =
            [
                new KyrolusEndpointConfig
                {
                    Name = EndpointNames.GetById,
                    Responses =
                    [
                        new KyrolusOpenApiResponse(StatusCodes.Status202Accepted, typeof(RuntimeLinkItem), "application/json")
                    ]
                }
            ]
        };
        group.MapGet("/endpoint-response/{id:guid}", (Guid id) => Results.Ok(new RuntimeLinkItem { Id = id, Name = "EndpointResponse" }))
            .ApplyOpenApi(endpointResponseConfig, EndpointNames.GetById);

        var endpointResponse = FindRouteEndpoint(app, "/api/openapi-runtime/endpoint-response/{id:guid}", HttpMethods.Get);
        if (endpointResponse is not null &&
            HasProducesStatus(endpointResponse, StatusCodes.Status202Accepted) &&
            HasOpenApiOperationMetadata(endpointResponse))
        {
            checks++;
        }

        var defaultResponseConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeDefaultResponse",
            Prefix = "api",
            Route = "runtime-default-response",
            DefaultResponses =
            [
                new KyrolusOpenApiResponse(StatusCodes.Status206PartialContent, typeof(IEnumerable<RuntimeLinkItem>), "application/json")
            ]
        };
        group.MapGet("/default-response", () => Results.Ok(Array.Empty<RuntimeLinkItem>()))
            .ApplyOpenApi(defaultResponseConfig, EndpointNames.GetAll);

        var defaultResponse = FindRouteEndpoint(app, "/api/openapi-runtime/default-response", HttpMethods.Get);
        if (defaultResponse is not null &&
            HasProducesStatus(defaultResponse, StatusCodes.Status206PartialContent))
        {
            checks++;
        }

        var fallbackConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeFallback",
            Prefix = "api",
            Route = "runtime-fallback",
            ViewModelType = typeof(RuntimeLinkItem),
            AuthorizeAllEndpoints = true,
            RateLimitPolicy = "runtime-rate-limit"
        };

        group.MapMethods("/head-response/{id:guid}", [HttpMethods.Head], () => Results.Ok())
            .ApplyOpenApi(fallbackConfig, EndpointNames.Head);

        var headResponse = FindRouteEndpoint(app, "/api/openapi-runtime/head-response/{id:guid}", HttpMethods.Head);
        if (headResponse is not null &&
            HasProducesStatus(headResponse, StatusCodes.Status200OK) &&
            HasProducesStatus(headResponse, StatusCodes.Status404NotFound) &&
            !HasProducesStatus(headResponse, StatusCodes.Status400BadRequest) &&
            HasProducesStatus(headResponse, StatusCodes.Status401Unauthorized) &&
            HasProducesStatus(headResponse, StatusCodes.Status403Forbidden) &&
            HasProducesStatus(headResponse, StatusCodes.Status429TooManyRequests))
        {
            checks++;
        }

        group.MapPost("/add-range", () => Results.Created())
            .ApplyOpenApi(fallbackConfig, EndpointNames.AddRange, typeof(IEnumerable<RuntimeLinkItem>));

        var addRange = FindRouteEndpoint(app, "/api/openapi-runtime/add-range", HttpMethods.Post);
        if (addRange is not null &&
            HasProducesStatus(addRange, StatusCodes.Status201Created) &&
            HasProducesStatus(addRange, StatusCodes.Status400BadRequest))
        {
            checks++;
        }

        group.MapPost("/query-seek", () => Results.Ok())
            .ApplyOpenApi(fallbackConfig, EndpointNames.QuerySeek);

        var querySeek = FindRouteEndpoint(app, "/api/openapi-runtime/query-seek", HttpMethods.Post);
        var querySeekProduces = querySeek?.Metadata.OfType<IProducesResponseTypeMetadata>()
            .FirstOrDefault(metadata => metadata.StatusCode == StatusCodes.Status200OK);
        if (querySeekProduces?.Type?.Name.Contains("KyrolusSeekResult", StringComparison.OrdinalIgnoreCase) == true)
        {
            checks++;
        }

        var parameterDocsMethod = typeof(KyrolusOpenApiMetadata).GetMethod(
            "ApplyParameterDocs",
            BindingFlags.Static | BindingFlags.NonPublic);
        var requestExamplesMethod = typeof(KyrolusOpenApiMetadata).GetMethod(
            "ApplyRequestExamples",
            BindingFlags.Static | BindingFlags.NonPublic);
        var resolveSuccessTypeMethod = typeof(KyrolusOpenApiMetadata).GetMethod(
            "ResolveSuccessType",
            BindingFlags.Static | BindingFlags.NonPublic)?.MakeGenericMethod(typeof(RuntimeLinkItem));
        var normalizeOperationIdPartMethod = typeof(KyrolusOpenApiMetadata).GetMethod(
            "NormalizeOperationIdPart",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (parameterDocsMethod is not null && requestExamplesMethod is not null)
        {
            var operation = new OpenApiOperation
            {
                Parameters =
                [
                    new OpenApiParameter { Name = "filter" },
                    new OpenApiParameter { Name = "includedProps" },
                    new OpenApiParameter { Name = "includeGraph" },
                    new OpenApiParameter { Name = "fields" },
                    new OpenApiParameter { Name = "cacheable" },
                    new OpenApiParameter { Name = "includeDeleted" },
                    new OpenApiParameter { Name = "pageNumber" },
                    new OpenApiParameter { Name = "pageSize" },
                    new OpenApiParameter { Name = "cursor" },
                    new OpenApiParameter { Name = "includeTotalCount" },
                    new OpenApiParameter { Name = "descending" },
                    new OpenApiParameter { Name = "unknown" }
                ],
                RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType()
                    }
                }
            };

            parameterDocsMethod.Invoke(null, [operation, EndpointNames.Query]);
            requestExamplesMethod.Invoke(null, [operation, EndpointNames.Query]);

            if (operation.Parameters.All(parameter => !string.IsNullOrWhiteSpace(parameter.Description)) &&
                !string.IsNullOrWhiteSpace(operation.Description) &&
                operation.RequestBody?.Content?["application/json"].Example is not null)
            {
                checks++;
            }

            var nonQueryOperation = new OpenApiOperation
            {
                RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType()
                    }
                }
            };

            requestExamplesMethod.Invoke(null, [nonQueryOperation, EndpointNames.GetAll]);
            if (nonQueryOperation.RequestBody?.Content?["application/json"].Example is null)
            {
                checks++;
            }
        }

        if (resolveSuccessTypeMethod is not null)
        {
            var viewModelConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
            {
                ApiName = "RuntimeTyped",
                Prefix = "api",
                Route = "runtime-typed",
                EndpointConfig =
                [
                    new KyrolusEndpointConfig
                    {
                        Name = EndpointNames.QueryPaged,
                        ViewModelType = typeof(RuntimeOpenApiProjection)
                    }
                ]
            };

            var bulkPatchType = resolveSuccessTypeMethod.Invoke(null, [fallbackConfig, EndpointNames.BulkPatch]) as Type;
            var countType = resolveSuccessTypeMethod.Invoke(null, [fallbackConfig, EndpointNames.Count]) as Type;
            var batchType = resolveSuccessTypeMethod.Invoke(null, [fallbackConfig, EndpointNames.Batch]) as Type;
            var queryPagedType = resolveSuccessTypeMethod.Invoke(null, [viewModelConfig, EndpointNames.QueryPaged]) as Type;
            var updateRangeType = resolveSuccessTypeMethod.Invoke(null, [fallbackConfig, EndpointNames.UpdateRange]) as Type;
            if (bulkPatchType == typeof(int) &&
                countType == typeof(long) &&
                batchType == typeof(object) &&
                updateRangeType?.IsGenericType == true &&
                updateRangeType.GetGenericArguments()[0] == typeof(RuntimeLinkItem) &&
                queryPagedType?.IsGenericType == true &&
                queryPagedType.GetGenericArguments()[0] == typeof(RuntimeOpenApiProjection))
            {
                checks++;
            }
        }

        if (normalizeOperationIdPartMethod is not null)
        {
            var fallbackOperationId = normalizeOperationIdPartMethod.Invoke(null, [null]);
            var normalizedOperationId = normalizeOperationIdPartMethod.Invoke(null, ["runtime link/item"]);
            if (Equals(fallbackOperationId, "KyrolusApi") &&
                Equals(normalizedOperationId, "runtime_link_item"))
            {
                checks++;
            }
        }

        if (endpointResponse is not null)
        {
            var transformerMetadata = endpointResponse.Metadata.First(metadata => metadata.GetType().Name == "KyrolusOpenApiOperationMetadata");
            var transformer = new KyrolusOpenApiOperationTransformer();
            var transformerOperation = new OpenApiOperation
            {
                Parameters =
                [
                    new OpenApiParameter { Name = "filter" },
                    new OpenApiParameter { Name = "fields" }
                ],
                RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType()
                    }
                }
            };
            var transformerContext = new OpenApiOperationTransformerContext
            {
                ApplicationServices = app.Services,
                DocumentName = "default",
                Description = new ApiDescription
                {
                    ActionDescriptor = new ActionDescriptor
                    {
                        EndpointMetadata = [transformerMetadata]
                    }
                }
            };
            await transformer.TransformAsync(transformerOperation, transformerContext, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(transformerOperation.OperationId) &&
                !string.IsNullOrWhiteSpace(transformerOperation.Parameters[0].Description) &&
                transformerOperation.RequestBody?.Content?["application/json"].Example is not null)
            {
                checks++;
            }

            var noMetadataOperation = new OpenApiOperation();
            var noMetadataContext = new OpenApiOperationTransformerContext
            {
                ApplicationServices = app.Services,
                DocumentName = "default",
                Description = new ApiDescription
                {
                    ActionDescriptor = new ActionDescriptor()
                }
            };
            await transformer.TransformAsync(noMetadataOperation, noMetadataContext, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(noMetadataOperation.OperationId))
            {
                checks++;
            }
        }

        await Task.Yield();
        return checks;
    }

    private static async Task<int> RunDefaultRouteMapperScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;
        var mapper = new DefaultRouteMapper<RuntimeLinkItem, RuntimeLinkItem, Guid>();
        var martenMapper = new KyrolusMartenRouteMapper<RuntimeLinkItem, RuntimeLinkItem, Guid>();

        using var versionedApp = WebApplication.CreateBuilder().Build();
        var versionedConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeMapped",
            Prefix = "api",
            Route = "runtime-link-item",
            ApiVersion = "2",
            VersionPrefix = "v",
            AppendVersionToPrefix = true,
            Endpoints = [EndpointNames.UpdateRange, EndpointNames.DeleteRange]
        };

        mapper.MapEndpoints(versionedApp, versionedConfig);

        var versionedPut = FindRouteEndpoint(versionedApp, "api/v2/runtime-link-items", HttpMethods.Put);
        var versionedDelete = FindRouteEndpoint(versionedApp, "api/v2/runtime-link-items", HttpMethods.Delete);
        if (versionedPut is not null &&
            versionedDelete is not null &&
            versionedPut.Metadata.GetMetadata<ITagsMetadata>()?.Tags.Contains("RuntimeMapped") == true)
        {
            checks++;
        }

        using var versionOnlyApp = WebApplication.CreateBuilder().Build();
        var versionOnlyConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeVersionOnly",
            Prefix = string.Empty,
            Route = "runtime-link-item",
            ApiVersion = "3",
            VersionPrefix = "v",
            AppendVersionToPrefix = true,
            Endpoints = [EndpointNames.GetAll]
        };

        mapper.MapEndpoints(versionOnlyApp, versionOnlyConfig);

        var versionOnlyGet = FindRouteEndpoint(versionOnlyApp, "v3/runtime-link-items", HttpMethods.Get);
        if (versionOnlyGet is not null)
        {
            checks++;
        }

        using var defaultedApp = WebApplication.CreateBuilder().Build();
        var defaultedConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = null!,
            Prefix = "api",
            Route = null!,
            AppendVersionToPrefix = false,
            Endpoints = [EndpointNames.GetAll, EndpointNames.GetById],
            EndpointConfig =
            [
                new KyrolusEndpointConfig
                {
                    Name = EndpointNames.GetById,
                    Authorize = true,
                    AuthorizationPolicy = "by-id-policy"
                }
            ]
        };

        mapper.MapEndpoints(defaultedApp, defaultedConfig);

        var defaultedGetAll = FindRouteEndpoint(defaultedApp, "api/RuntimeLinkItems", HttpMethods.Get);
        var defaultedGetById = FindRouteEndpoint(defaultedApp, "api/RuntimeLinkItems/{id}", HttpMethods.Get);
        if (defaultedGetAll is not null &&
            defaultedGetById is not null &&
            defaultedConfig.ApiName == nameof(RuntimeLinkItem) &&
            defaultedConfig.Route == nameof(RuntimeLinkItem) &&
            defaultedGetAll.Metadata.GetMetadata<ITagsMetadata>()?.Tags.Contains(nameof(RuntimeLinkItem)) == true &&
            !HasAuthorizationMetadata(defaultedGetAll) &&
            HasAuthorizationPolicy(defaultedGetById, "by-id-policy"))
        {
            checks++;
        }

        using var excludedApp = WebApplication.CreateBuilder().Build();
        var excludedConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "ExcludedRuntime",
            Prefix = string.Empty,
            Route = "excluded-runtime",
            AppendVersionToPrefix = false,
            AllEndpointsExcept = [EndpointNames.All, EndpointNames.DeleteRange],
            AuthorizeAllEndpoints = true,
            GeneralAuthorizationPolicy = "general-policy"
        };

        mapper.MapEndpoints(excludedApp, excludedConfig);

        var excludedGet = FindRouteEndpoint(excludedApp, "excluded-runtimes", HttpMethods.Get);
        var excludedDeleteRange = FindRouteEndpoint(excludedApp, "excluded-runtimes", HttpMethods.Delete);
        if (excludedGet is not null &&
            excludedDeleteRange is null &&
            HasAuthorizationPolicy(excludedGet, "general-policy"))
        {
            checks++;
        }

        using var prefixOnlyApp = WebApplication.CreateBuilder().Build();
        var prefixOnlyConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "PrefixOnly",
            Prefix = "tenant",
            Route = "prefix-only",
            AppendVersionToPrefix = false,
            Endpoints = [EndpointNames.Add]
        };

        mapper.MapEndpoints(prefixOnlyApp, prefixOnlyConfig);

        var prefixOnlyPost = FindRouteEndpoint(prefixOnlyApp, "tenant/prefix-onlys", HttpMethods.Post);
        if (prefixOnlyPost is not null)
        {
            checks++;
        }

        using var martenHeadApp = WebApplication.CreateBuilder().Build();
        var martenHeadConfig = new KyrolusMartenApiConfig<RuntimeLinkItem>
        {
            ApiName = "MartenHead",
            Prefix = "api",
            Route = "marten-head-item",
            AppendVersionToPrefix = false,
            Endpoints = [EndpointNames.GetById],
            EnableHeadEndpoint = true,
            AuthorizeAllEndpoints = true,
            GeneralAuthorizationPolicy = "head-general-policy"
        };

        martenMapper.MapEndpoints(martenHeadApp, martenHeadConfig);

        var martenHead = FindRouteEndpoint(martenHeadApp, "api/marten-head-items/{id}", HttpMethods.Head);
        var martenGetById = FindRouteEndpoint(martenHeadApp, "api/marten-head-items/{id}", HttpMethods.Get);
        if (martenHead is not null &&
            martenGetById is not null &&
            HasAuthorizationPolicy(martenHead, "head-general-policy"))
        {
            checks++;
        }

        using var compositeKeyOnlyApp = WebApplication.CreateBuilder().Build();
        var compositeKeyOnlyConfig = new KyrolusMartenApiConfig<RuntimeLinkItem>
        {
            ApiName = "CompositeOnly",
            Prefix = "api",
            Route = "composite-only-item",
            AppendVersionToPrefix = false,
            Endpoints = [EndpointNames.All],
            AllEndpointsExcept = [EndpointNames.DeleteRange],
            CompositeKeyOnly = true,
            EnableHeadEndpoint = true
        };

        martenMapper.MapEndpoints(compositeKeyOnlyApp, compositeKeyOnlyConfig);

        if (FindRouteEndpoint(compositeKeyOnlyApp, "api/composite-only-items/{id}", HttpMethods.Get) is null &&
            FindRouteEndpoint(compositeKeyOnlyApp, "api/composite-only-items/{id}", HttpMethods.Head) is null &&
            FindRouteEndpoint(compositeKeyOnlyApp, "api/composite-only-items/{id}", HttpMethods.Put) is null &&
            FindRouteEndpoint(compositeKeyOnlyApp, "api/composite-only-items/{id}", HttpMethods.Patch) is null &&
            FindRouteEndpoint(compositeKeyOnlyApp, "api/composite-only-items/{id}", HttpMethods.Delete) is null &&
            FindRouteEndpoint(compositeKeyOnlyApp, "api/composite-only-items", HttpMethods.Get) is not null &&
            compositeKeyOnlyConfig.AllEndpointsExcept.SequenceEqual([EndpointNames.DeleteRange]))
        {
            checks++;
        }

        await Task.Yield();
        return checks;
    }

    private static RouteEndpoint? FindRouteEndpoint(WebApplication app, string routePattern, string httpMethod)
    {
        var routeBuilder = (IEndpointRouteBuilder)app;
        foreach (var endpoint in routeBuilder.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
        {
            if (!string.Equals(endpoint.RoutePattern.RawText, routePattern, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (methods is null)
            {
                continue;
            }

            if (methods.Any(method => string.Equals(method, httpMethod, StringComparison.OrdinalIgnoreCase)))
            {
                return endpoint;
            }
        }

        return null;
    }

    private static bool HasAuthorizationMetadata(RouteEndpoint endpoint)
    {
        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
    }

    private static bool HasAuthorizationPolicy(RouteEndpoint endpoint, string? policy)
    {
        var metadata = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        if (metadata.Count == 0)
        {
            return false;
        }

        return metadata.Any(entry => string.Equals(entry.Policy, policy, StringComparison.Ordinal));
    }

    private static bool HasProducesStatus(RouteEndpoint endpoint, int statusCode)
    {
        return endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()
            .Any(metadata => metadata.StatusCode == statusCode);
    }

    private static bool HasOpenApiOperationMetadata(RouteEndpoint endpoint)
    {
        return endpoint.Metadata.Any(metadata => string.Equals(
            metadata.GetType().Name,
            "KyrolusOpenApiOperationMetadata",
            StringComparison.Ordinal));
    }

}
