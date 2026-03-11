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
    private static async Task<int> RunFluentValidationScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        var invalidRequest = new RuntimeFluentValidationProbeRequest
        {
            Name = string.Empty,
            CreatedBy = 0,
            Id = 0,
            Description = "too-long",
            Color = "red",
            Tags = Array.Empty<string>(),
            Url = "notaurl",
            StrictUrl = "still-not-a-url"
        };

        var validRequest = new RuntimeFluentValidationProbeRequest
        {
            Name = "valid",
            CreatedBy = 7,
            Id = 11,
            Description = "short",
            Color = "#A1B2C3",
            Tags = ["tag"],
            Url = "https://example.com",
            OptionalUrl = null,
            StrictUrl = "https://strict.example.com"
        };

        var services = new ServiceCollection();
        services.AddKyrolusFluentValidation();
        services.AddTransient<IValidator<RuntimeFluentValidationProbeRequest>, RuntimeFluentValidationProbeValidator>();

        using var provider = services.BuildServiceProvider();
        var requestValidator = provider.GetServices<IKyrolusRequestValidator<RuntimeFluentValidationProbeRequest>>().Single();
        var contextualValidator = (IKyrolusRequestValidatorWithContext<RuntimeFluentValidationProbeRequest>)requestValidator;

        var invalidFailures = await requestValidator.ValidateAsync(invalidRequest, cancellationToken).ConfigureAwait(false);
        if (invalidFailures.Count >= 7 &&
            invalidFailures.Any(failure => failure.PropertyName == nameof(RuntimeFluentValidationProbeRequest.Name) &&
                                           failure.Group == "api" &&
                                           failure.Severity == KyrolusValidationSeverity.Warning &&
                                           failure.MessageKey == "name.required") &&
            invalidFailures.Any(failure => failure.PropertyName == nameof(RuntimeFluentValidationProbeRequest.CreatedBy) &&
                                           failure.Group == "audit") &&
            invalidFailures.Any(failure => failure.PropertyName == nameof(RuntimeFluentValidationProbeRequest.Id) &&
                                           failure.Group == "identity") &&
            invalidFailures.Any(failure => failure.PropertyName == nameof(RuntimeFluentValidationProbeRequest.Description) &&
                                           failure.Metadata is { Count: > 0 } &&
                                           failure.Metadata.ContainsKey("MaxLength")) &&
            invalidFailures.Any(failure => failure.PropertyName == "payload.url"))
        {
            checks++;
        }

        var strictFailures = await contextualValidator.ValidateAsync(
            invalidRequest,
            new KyrolusValidationContext(RuleSets: ["strict"]),
            cancellationToken).ConfigureAwait(false);
        if (strictFailures.Count == 1 &&
            strictFailures[0].PropertyName == nameof(RuntimeFluentValidationProbeRequest.StrictUrl) &&
            strictFailures[0].Severity == KyrolusValidationSeverity.Info &&
            strictFailures[0].Group == "strict-group" &&
            strictFailures[0].RuleSet == "strict")
        {
            checks++;
        }

        var validFailures = await requestValidator.ValidateAsync(validRequest, cancellationToken).ConfigureAwait(false);
        if (validFailures.Count == 0)
        {
            checks++;
        }

        using var noValidatorProvider = new ServiceCollection()
            .AddKyrolusFluentValidation()
            .BuildServiceProvider();
        var noValidator = noValidatorProvider.GetServices<IKyrolusRequestValidator<RuntimeNoValidatorFluentProbeRequest>>().Single();
        var noValidatorFailures = await noValidator.ValidateAsync(new RuntimeNoValidatorFluentProbeRequest(), cancellationToken).ConfigureAwait(false);
        if (noValidatorFailures.Count == 0)
        {
            checks++;
        }

        var behavior = new KyrolusValidationBehavior<RuntimeFluentValidationProbeRequest, string>(
            provider.GetServices<IKyrolusRequestValidator<RuntimeFluentValidationProbeRequest>>());
        var nextCalls = 0;
        var nextResult = await behavior.Handle(
            validRequest,
            () =>
            {
                nextCalls++;
                return Task.FromResult("validated");
            },
            cancellationToken).ConfigureAwait(false);
        if (nextCalls == 1 && nextResult == "validated")
        {
            checks++;
        }

        await ExpectThrowsAsync<KyrolusSous.Validation.Abstractions.KyrolusValidationException>(() => behavior.Handle(
            invalidRequest,
            () => Task.FromResult("should-not-run"),
            cancellationToken)).ConfigureAwait(false);
        checks++;

        var passThroughBehavior = new KyrolusValidationBehavior<RuntimeNoValidatorFluentProbeRequest, string>(
            Array.Empty<IKyrolusRequestValidator<RuntimeNoValidatorFluentProbeRequest>>());
        var passThroughCalls = 0;
        var passThroughResult = await passThroughBehavior.Handle(
            new RuntimeNoValidatorFluentProbeRequest(),
            () =>
            {
                passThroughCalls++;
                return Task.FromResult("pass-through");
            },
            cancellationToken).ConfigureAwait(false);
        if (passThroughCalls == 1 && passThroughResult == "pass-through")
        {
            checks++;
        }

        return checks;
    }

}
