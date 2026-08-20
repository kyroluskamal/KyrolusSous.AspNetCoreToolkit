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
    private static async Task<int> RunExceptionHandlingScenariosAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var checks = 0;

        using var scope = serviceProvider.CreateScope();
        var scoped = scope.ServiceProvider;

        var dictionaryLocalizer = new KyrolusDictionaryErrorLocalizer(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bad_request"] = "Localized bad request"
        });
        if (dictionaryLocalizer.Localize("bad_request", "fallback", CultureInfo.GetCultureInfo("en-US")) == "Localized bad request" &&
            dictionaryLocalizer.Localize(string.Empty, "fallback", CultureInfo.GetCultureInfo("en-US")) == "fallback")
        {
            checks++;
        }

        var nullLocalizer = new KyrolusNullErrorLocalizer();
        if (nullLocalizer.Localize("code", "fallback", CultureInfo.InvariantCulture) == "fallback")
        {
            checks++;
        }

        var stringLocalizer = new RuntimeStringLocalizer(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code.one"] = "Translated one"
        });
        var stringErrorLocalizer = new KyrolusStringLocalizerErrorLocalizer(stringLocalizer);
        if (stringErrorLocalizer.Localize("code.one", "fallback", CultureInfo.GetCultureInfo("en-US")) == "Translated one" &&
            stringErrorLocalizer.Localize("unknown.code", "fallback", CultureInfo.GetCultureInfo("en-US")) == "fallback")
        {
            checks++;
        }

        var allowListSanitizer = new KyrolusDefaultErrorMetadataSanitizer(Options.Create(new KyrolusExceptionHandlingOptions
        {
            MetadataAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "allowed" },
            SanitizeMetadata = true
        }));
        var allowListMetadata = allowListSanitizer.Sanitize(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["allowed"] = "ok",
                ["password"] = "secret"
            },
            new KyrolusErrorContext("trace-1", null, null, null, null, null, null));
        if (allowListMetadata.Count == 1 && allowListMetadata.ContainsKey("allowed"))
        {
            checks++;
        }

        var mappingService = scoped.GetRequiredService<KyrolusExceptionMappingService>();
        var metadataSanitizer = scoped.GetRequiredService<IKyrolusErrorMetadataSanitizer>();
        var translator = new KyrolusExceptionTranslator(
            mappingService,
            metadataSanitizer,
            new RuntimeHostEnvironment("Development"),
            Options.Create(new KyrolusExceptionHandlingOptions
            {
                IncludeExceptionDetailsInResponse = true,
                IncludeContextMetadata = true,
                IncludeTraceId = true,
                IncludeExceptionDetailsInDevelopment = true
            }));

        var translatorContext = new KyrolusErrorContext(
            "trace-translator",
            "corr-translator",
            "user-translator",
            "tenant-translator",
            "/api/runtime/errors",
            HttpMethods.Post,
            CultureInfo.GetCultureInfo("en-US"));

        var translatedUnhandled = translator.Translate(
            new InvalidOperationException("Unhandled runtime error", new Exception("Inner runtime error")),
            translatorContext,
            includeDetails: true);
        if (translatedUnhandled.StatusCode == HttpStatusCode.InternalServerError &&
            translatedUnhandled.Error.Metadata is { Count: > 0 } metadata &&
            metadata.ContainsKey("exceptionType") &&
            metadata.ContainsKey("innerException") &&
            metadata.ContainsKey("correlationId"))
        {
            checks++;
        }

        var translatedBadRequest = translator.Translate(
            new KyrolusBadRequestException("Bad request title", "Bad request detail"),
            translatorContext,
            includeDetails: false);
        if (translatedBadRequest.StatusCode == HttpStatusCode.BadRequest &&
            string.Equals(translatedBadRequest.Error.Code, KyrolusErrorCodes.BadRequest, StringComparison.Ordinal))
        {
            checks++;
        }

        var accessor = new HttpContextAccessor();
        var options = scoped.GetRequiredService<IOptions<KyrolusExceptionHandlingOptions>>();

        var cultureContext = new DefaultHttpContext();
        cultureContext.TraceIdentifier = "trace-context";
        cultureContext.Request.Path = "/api/runtime/context";
        cultureContext.Request.Method = HttpMethods.Get;
        cultureContext.Request.Headers[options.Value.CorrelationIdHeaderName] = "corr-context";
        cultureContext.Request.Headers["Accept-Language"] = "en-US";
        cultureContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "runtime-user"),
                new Claim("tenant_id", "runtime-tenant")
            ],
            "runtime-auth"));
        accessor.HttpContext = cultureContext;

        var contextFactory = new KyrolusHttpErrorContextFactory(accessor, options);
        var resolvedContext = contextFactory.Create(new Exception("context"));
        if (resolvedContext.CorrelationId == "corr-context" &&
            resolvedContext.UserId == "runtime-user" &&
            resolvedContext.TenantId == "runtime-tenant" &&
            resolvedContext.Culture?.Name == "en-US")
        {
            checks++;
        }

        cultureContext.Request.Headers["Accept-Language"] = "___invalid-culture___";
        var invalidCultureContext = contextFactory.Create(new Exception("invalid-culture"));
        if (invalidCultureContext.Culture is null)
        {
            checks++;
        }

        var filterContextHttp = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        filterContextHttp.TraceIdentifier = "trace-filter";
        filterContextHttp.Request.Path = "/api/runtime/filter";
        filterContextHttp.Request.Method = HttpMethods.Post;
        filterContextHttp.Request.Headers[options.Value.CorrelationIdHeaderName] = "corr-filter";
        filterContextHttp.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "filter-user"),
                new Claim("tenant_id", "filter-tenant")
            ],
            "filter-auth"));

        accessor.HttpContext = filterContextHttp;
        var filter = new KyrolusExceptionFilter(
            mappingService,
            scoped.GetRequiredService<IKyrolusErrorResponseWriter>(),
            contextFactory,
            metadataSanitizer,
            scoped.GetRequiredService<IHostEnvironment>(),
            options,
            scoped.GetRequiredService<ILogger<KyrolusExceptionFilter>>());

        var actionContext = new ActionContext(filterContextHttp, new RouteData(), new ActionDescriptor());
        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = new KyrolusBadRequestException("filter bad request", "invalid input")
        };
        await filter.OnExceptionAsync(exceptionContext).ConfigureAwait(false);
        if (exceptionContext.ExceptionHandled &&
            exceptionContext.Result is EmptyResult &&
            filterContextHttp.Response.StatusCode == StatusCodes.Status400BadRequest)
        {
            checks++;
        }

        var alreadyHandled = new ExceptionContext(actionContext, [])
        {
            Exception = new Exception("ignored"),
            ExceptionHandled = true
        };
        await filter.OnExceptionAsync(alreadyHandled).ConfigureAwait(false);
        if (alreadyHandled.ExceptionHandled)
        {
            checks++;
        }

        using var loggerFactory = LoggerFactory.Create(_ => { });

        var registeredServices = new ServiceCollection()
            .AddLogging()
            .AddKyrolusExceptionHandling();
        using var registeredProvider = registeredServices.BuildServiceProvider();
        if (registeredProvider.GetRequiredService<IHttpContextAccessor>() is HttpContextAccessor &&
            registeredProvider.GetRequiredService<KyrolusHttpErrorContextFactory>() is not null &&
            registeredProvider.GetRequiredService<KyrolusExceptionMappingService>() is not null &&
            registeredProvider.GetRequiredService<IKyrolusErrorResponseWriter>() is KyrolusJsonErrorResponseWriter &&
            registeredProvider.GetServices<IKyrolusExceptionMapper>().Count() >= 3)
        {
            checks++;
        }

        var fluentExceptionServices = new ServiceCollection();
        if (ReferenceEquals(fluentExceptionServices, fluentExceptionServices.AddKyrolusFluentValidationExceptionHandling()) &&
            fluentExceptionServices.Any(descriptor =>
                descriptor.ServiceType == typeof(IKyrolusExceptionMapper) &&
                descriptor.ImplementationType == typeof(KyrolusFluentValidationExceptionMapper)))
        {
            checks++;
        }

        var fluentExceptionMapper = new KyrolusFluentValidationExceptionMapper();
        var fluentContext = new KyrolusErrorContext("trace-fluent", null, null, null, null, null, CultureInfo.InvariantCulture);
        var fluentException = new ValidationException("Validation failed",
        [
            new ValidationFailure("Name", "Name is required")
            {
                ErrorCode = "NotEmptyValidator"
            }
        ]);
        if (fluentExceptionMapper.Order == -50 &&
            fluentExceptionMapper.TryMap(fluentException, fluentContext, out var fluentMapping) &&
            fluentMapping.StatusCode == HttpStatusCode.BadRequest &&
            fluentMapping.Error.Code == KyrolusErrorCodes.Validation &&
            fluentMapping.Error.Errors is { Count: 1 } &&
            fluentMapping.Error.Errors[0].Field == "Name" &&
            fluentMapping.Error.Errors[0].Code == "NotEmptyValidator" &&
            !fluentExceptionMapper.TryMap(new InvalidOperationException("not-fluent"), fluentContext, out _))
        {
            checks++;
        }

        var cqrsExceptionServices = new ServiceCollection();
        if (ReferenceEquals(cqrsExceptionServices, cqrsExceptionServices.AddKyrolusCqrsExceptionHandling()) &&
            cqrsExceptionServices.Any(descriptor =>
                descriptor.ServiceType == typeof(KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusPipelineBehavior<,>) &&
                descriptor.ImplementationType == typeof(KyrolusExceptionMappingBehavior<,>)))
        {
            checks++;
        }

        var cqrsMappedBehavior = new KyrolusExceptionMappingBehavior<string, string>(
            [new RuntimeMappedCqrsExceptionMapper<string, InvalidOperationException>("mapped-response")]);
        if (await cqrsMappedBehavior.Handle(
                "mapped-request",
                _ => Task.FromException<string>(new InvalidOperationException("mapped")),
                cancellationToken).ConfigureAwait(false) == "mapped-response" &&
            await cqrsMappedBehavior.Handle(
                "pass-through",
                _ => Task.FromResult("ok"),
                cancellationToken).ConfigureAwait(false) == "ok")
        {
            checks++;
        }

        await ExpectThrowsAsync<ArgumentException>(
            () => cqrsMappedBehavior.Handle(
                "unmapped-request",
                _ => Task.FromException<string>(new ArgumentException("unmapped")),
                cancellationToken),
            "CQRS exception mapping behavior should rethrow exceptions that no mapper handles.").ConfigureAwait(false);
        checks++;

        using var dictionaryLocalizationProvider = new ServiceCollection()
            .AddKyrolusExceptionHandlingLocalization(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["localized.code"] = "Localized title"
            })
            .BuildServiceProvider();
        if (dictionaryLocalizationProvider.GetRequiredService<IKyrolusErrorLocalizer>()
            .Localize("localized.code", "fallback", CultureInfo.InvariantCulture) == "Localized title")
        {
            checks++;
        }

        using var typedLocalizationProvider = new ServiceCollection()
            .AddSingleton<IStringLocalizer<RuntimeExceptionResource>>(
                new RuntimeTypedStringLocalizer<RuntimeExceptionResource>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["typed.code"] = "Typed title"
                }))
            .AddKyrolusExceptionHandlingLocalization<RuntimeExceptionResource>()
            .BuildServiceProvider();
        if (typedLocalizationProvider.GetRequiredService<IKyrolusErrorLocalizer>()
            .Localize("typed.code", "fallback", CultureInfo.InvariantCulture) == "Typed title")
        {
            checks++;
        }

        var appBuilder = new ApplicationBuilder(registeredProvider);
        if (ReferenceEquals(appBuilder.UseKyrolusExceptionHandling(), appBuilder))
        {
            checks++;
        }

        var fallbackMappingService = new KyrolusExceptionMappingService(Array.Empty<IKyrolusExceptionMapper>());
        var fallbackMapping = fallbackMappingService.Map(new InvalidOperationException("fallback"), translatorContext);
        if (fallbackMapping.StatusCode == HttpStatusCode.InternalServerError &&
            fallbackMapping.Error.Code == KyrolusErrorCodes.InternalError)
        {
            checks++;
        }

        var localizedMappingService = new KyrolusExceptionMappingService(
            [new KyrolusFrameworkExceptionMapper()],
            new KyrolusDictionaryErrorLocalizer(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [KyrolusErrorCodes.Unauthorized] = "Localized unauthorized",
                [$"{KyrolusErrorCodes.Unauthorized}.detail"] = "Localized unauthorized detail"
            }));
        var localizedUnauthorized = localizedMappingService.Map(new UnauthorizedAccessException("denied"), translatorContext);
        if (localizedUnauthorized.StatusCode == HttpStatusCode.Unauthorized &&
            localizedUnauthorized.Error.Title == "Localized unauthorized" &&
            localizedUnauthorized.Error.Detail == "Localized unauthorized detail")
        {
            checks++;
        }

        var frameworkMapper = new KyrolusFrameworkExceptionMapper();
        var frameworkCases = new (Exception Exception, HttpStatusCode StatusCode, string ErrorCode, bool IsTransient)[]
        {
            (new TimeoutException("timeout"), HttpStatusCode.GatewayTimeout, KyrolusErrorCodes.Timeout, true),
            (new TaskCanceledException("task-cancelled"), HttpStatusCode.RequestTimeout, KyrolusErrorCodes.Cancelled, true),
            (new OperationCanceledException("cancelled"), HttpStatusCode.RequestTimeout, KyrolusErrorCodes.Cancelled, true),
            (new HttpRequestException("external", null, HttpStatusCode.BadGateway), HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, true),
            (new HttpRequestException("external"), HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, true),
            (new SocketException((int)SocketError.ConnectionRefused), HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, true),
            (new JsonException("json"), HttpStatusCode.BadRequest, KyrolusErrorCodes.InvalidJson, false),
            (new ArgumentException("arg"), HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, false),
            (new NotSupportedException("unsupported"), HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, false)
        };
        if (frameworkCases.All(testCase =>
                frameworkMapper.TryMap(testCase.Exception, translatorContext, out var mapped) &&
                mapped.StatusCode == testCase.StatusCode &&
                mapped.Error.Code == testCase.ErrorCode &&
                mapped.IsTransient == testCase.IsTransient &&
                mapped.Error.TraceId == translatorContext.TraceId))
        {
            checks++;
        }

        if (!frameworkMapper.TryMap(new InvalidOperationException("not-mapped"), translatorContext, out _))
        {
            checks++;
        }

        var translatorDefaultContext = translator.Translate(new InvalidOperationException("default-context"));
        if (translatorDefaultContext.StatusCode == HttpStatusCode.InternalServerError &&
            translatorDefaultContext.Error.Code == KyrolusErrorCodes.InternalError)
        {
            checks++;
        }

        var productionTranslator = new KyrolusExceptionTranslator(
            localizedMappingService,
            metadataSanitizer,
            new RuntimeHostEnvironment("Production"),
            Options.Create(new KyrolusExceptionHandlingOptions
            {
                IncludeExceptionDetailsInResponse = false,
                IncludeContextMetadata = false,
                IncludeTraceId = false,
                IncludeExceptionDetailsInDevelopment = true
            }));
        var translatedProduction = productionTranslator.Translate(new InvalidOperationException("prod"));
        if (translatedProduction.Error.Metadata is null or { Count: 0 })
        {
            checks++;
        }

        var writerContext = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        var jsonWriter = new KyrolusJsonErrorResponseWriter();
        var writerMapping = new KyrolusExceptionMapping(
            new KyrolusErrorEnvelope("writer_code", "Writer title", "Writer detail", "trace-writer"),
            HttpStatusCode.Conflict);
        await jsonWriter.WriteAsync(writerContext, writerMapping, translatorContext, cancellationToken).ConfigureAwait(false);
        writerContext.Response.Body.Position = 0;
        var writerBody = await new StreamReader(writerContext.Response.Body).ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (writerContext.Response.StatusCode == StatusCodes.Status409Conflict &&
            writerContext.Response.ContentType == "application/json" &&
            writerBody.Contains("\"code\":\"writer_code\"", StringComparison.Ordinal))
        {
            checks++;
        }

        static DefaultHttpContext CreateExceptionHandlerContext()
            => new()
            {
                Response =
                {
                    Body = new MemoryStream()
                }
            };

        var authenticationHandler = new AuthenticationExceptionHandler(loggerFactory.CreateLogger<AuthenticationExceptionHandler>());
        var authenticationContext = CreateExceptionHandlerContext();
        if (await authenticationHandler.TryHandleAsync(authenticationContext, new SslAuthenticationException("ssl"), cancellationToken).ConfigureAwait(false) &&
            authenticationContext.Response.StatusCode == StatusCodes.Status502BadGateway &&
            !await authenticationHandler.TryHandleAsync(CreateExceptionHandlerContext(), new InvalidOperationException("ignored"), cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var unauthorizedHandler = new UnauthorizedExceptionHandler(loggerFactory.CreateLogger<UnauthorizedExceptionHandler>());
        var unauthorizedContext = CreateExceptionHandlerContext();
        if (await unauthorizedHandler.TryHandleAsync(unauthorizedContext, new UnauthorizedException("unauthorized"), cancellationToken).ConfigureAwait(false) &&
            unauthorizedContext.Response.StatusCode == StatusCodes.Status401Unauthorized &&
            !await unauthorizedHandler.TryHandleAsync(CreateExceptionHandlerContext(), new InvalidOperationException("ignored"), cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var notFoundHandler = new NotFoundExceptionHandler(loggerFactory.CreateLogger<NotFoundExceptionHandler>());
        var notFoundContext = CreateExceptionHandlerContext();
        if (await notFoundHandler.TryHandleAsync(notFoundContext, new NotFoundException("missing"), cancellationToken).ConfigureAwait(false) &&
            notFoundContext.Response.StatusCode == StatusCodes.Status404NotFound &&
            !await notFoundHandler.TryHandleAsync(CreateExceptionHandlerContext(), new InvalidOperationException("ignored"), cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var valEx = new ValidationException([new ValidationFailure("Name", "Name is required")]);
        var validationHandler = new ValidationExceptionHandler(loggerFactory.CreateLogger<ValidationExceptionHandler>());
        var validationContext = CreateExceptionHandlerContext();
        if (await validationHandler.TryHandleAsync(validationContext, valEx, cancellationToken).ConfigureAwait(false) &&
            validationContext.Response.StatusCode == StatusCodes.Status400BadRequest &&
            !await validationHandler.TryHandleAsync(CreateExceptionHandlerContext(), new InvalidOperationException("ignored"), cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var socketHandler = new SocketExceptionHandler(loggerFactory.CreateLogger<SocketExceptionHandler>());
        var socketContext = CreateExceptionHandlerContext();
        if (await socketHandler.TryHandleAsync(socketContext, new SocketException((int)SocketError.HostNotFound), cancellationToken).ConfigureAwait(false) &&
            socketContext.Response.StatusCode == StatusCodes.Status500InternalServerError &&
            !await socketHandler.TryHandleAsync(CreateExceptionHandlerContext(), new InvalidOperationException("ignored"), cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var npgsqlHandler = new NpgsqlExceptionHandler(loggerFactory.CreateLogger<NpgsqlExceptionHandler>());
        var npgsqlContext = CreateExceptionHandlerContext();
        if (await npgsqlHandler.TryHandleAsync(
                npgsqlContext,
                new PostgresException("npgsql", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure),
                cancellationToken).ConfigureAwait(false) &&
            npgsqlContext.Response.StatusCode == StatusCodes.Status500InternalServerError &&
            !await npgsqlHandler.TryHandleAsync(CreateExceptionHandlerContext(), new InvalidOperationException("ignored"), cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var generalHandler = new GeneralExceptionHandler(loggerFactory.CreateLogger<GeneralExceptionHandler>());
        var generalContext = CreateExceptionHandlerContext();
        if (await generalHandler.TryHandleAsync(generalContext, new Exception("general"), cancellationToken).ConfigureAwait(false) &&
            generalContext.Response.StatusCode == StatusCodes.Status400BadRequest)
        {
            checks++;
        }

        await Task.Yield();
        return checks;
    }

}
