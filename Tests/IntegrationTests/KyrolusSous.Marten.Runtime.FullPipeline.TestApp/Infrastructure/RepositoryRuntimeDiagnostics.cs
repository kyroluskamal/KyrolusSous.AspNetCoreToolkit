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
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.FluentValidation;
using KyrolusSous.ExceptionHandling.Runtime;
using KyrolusSous.ExceptionHandling.Runtime.ClasesAndHelpers;
using KyrolusSous.ExceptionHandling.Runtime.Handlers;
using KyrolusSous.ExceptionHandling.Runtime.Interfaces;
using KyrolusSous.ExceptionHandling.Runtime.Mapping;
using KyrolusSous.ExceptionHandling.Runtime.Writers;
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

public sealed record RepositoryRuntimeDiagnosticsRequest(string Mode = "menu-runtime");

public sealed record RepositoryRuntimeDiagnosticsResponse(
    string Mode,
    int? AllFirstCount = null,
    int? AllSecondCount = null,
    bool? ByIdFirstFound = null,
    bool? ByIdSecondFound = null,
    int? CrossTenantCount = null,
    bool? ExistsAny = null,
    int? StreamCount = null,
    int? QueryCount = null,
    int? QueryPageCount = null,
    int? PageCount = null,
    int? CompiledCountFirst = null,
    int? CompiledCountSecond = null,
    int? WithSessionValue = null,
    int? TransformResult = null,
    bool? RemovedEntity = null,
    bool? RemovedById = null,
    bool? RemovedRange = null,
    bool? PatchResultFound = null,
    string? ResolvedFromResolver = null,
    string? ResolvedFromNullResolver = null,
    bool? IncludedPayment = null,
    int? IncludedPaymentsCount = null,
    int? IncludedPaymentArrayCount = null,
    int? IncludedPaymentSetCount = null,
    bool? NullIncludeHandled = null,
    int? SoftDeleteActiveCountBefore = null,
    int? SoftDeleteIncludingDeletedCountBefore = null,
    int? SoftDeleteDeletedOnlyCountBefore = null,
    int? SoftDeleteActiveCountAfterRestore = null,
    bool? SoftDeleteByIdDeletedFilteredOut = null,
    bool? SoftDeleteByIdIncludingDeletedFound = null,
    bool? SoftDeleteDisabledPolicyReturnsEmpty = null,
    bool? SoftDeleteInvalidPolicyReturnsEmpty = null,
    int? SoftDeleteDeleteWhereResult = null,
    int? SoftDeleteRestoreWhereResult = null,
    bool? SoftDeleteRestoreById = null,
    bool? SoftDeleteRestoreRange = null,
    int? AuthorizationChecks = null,
    int? ValidationChecks = null,
    int? TracingChecks = null,
    int? ObserverChecks = null,
    int? ResilienceChecks = null,
    int? SpecificationChecks = null,
    int? QueryPrimitiveChecks = null,
    int? SagaChecks = null,
    int? EventStoreChecks = null,
    int? ProjectionManagerChecks = null,
    int? ProjectionOrchestratorChecks = null,
    int? RuntimeRegistrationChecks = null,
    int? CqrsHandlerChecks = null,
    int? EndpointKitCoreChecks = null,
    int? EndpointKitMartenChecks = null,
    int? ValidationRuntimeChecks = null,
    int? ExceptionHandlingChecks = null,
    int? CacheAbstractionsChecks = null,
    int? DataProtectionChecks = null,
    int? MediatorChecks = null,
    int? LoggingChecks = null,
    int? RedisCacheChecks = null,
    int? RedisFallbackChecks = null,
    int? DataProtectionRedisChecks = null,
    int? ExceptionAbstractionsChecks = null,
    int? DbProbeCount = null);

public static partial class RepositoryRuntimeDiagnostics
{
}


