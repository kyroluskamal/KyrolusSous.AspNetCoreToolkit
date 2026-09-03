using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Mediator.Generator.UnitTests;

/// <summary>
/// Regression coverage for three bugs found in a review of the generator's open-generic handling:
/// <list type="number">
/// <item><description>
/// SMG004 used to key duplicate detection on the handler interface shape alone (e.g.
/// <c>IKyrolusCommandHandler&lt;,&gt;</c>), so two legitimately different open-generic handlers that
/// close over different open request types - <c>CreateHandler&lt;T&gt; : IKyrolusCommandHandler&lt;CreateCommand&lt;T&gt;, T&gt;</c>
/// vs <c>UpdateHandler&lt;T&gt; : IKyrolusCommandHandler&lt;UpdateCommand&lt;T&gt;, T&gt;</c> - were
/// wrongly reported as duplicates. Fixed by keying on the canonicalised request/response shape too.
/// </description></item>
/// <item><description>
/// Even without the diagnostic, the emitted registration used <c>TryAddTransient(typeof(open
/// interface), typeof(open handler))</c>, which dedupes on service type alone - so only the first of
/// several distinct open-generic handlers under the same open interface ever actually registered.
/// Fixed by switching to <c>TryAddEnumerable</c>, which dedupes on (service type, implementation
/// type) instead.
/// </description></item>
/// <item><description>
/// <c>ComputeClosedUserOpenBehaviorRegistrations</c> alphabetically sorted the closed
/// <c>AddOpenBehavior</c> registration lines before emitting them, unrelated to the order the
/// behaviors were actually added in source - so two behaviors sharing a <c>[PipelineOrder]</c> value
/// could run in a different relative order under the generator (AOT) than under the reflection/JIT
/// path for the same source. Fixed by preserving call-site discovery order instead.
/// </description></item>
/// </list>
/// </summary>
public sealed class OpenGenericHandlerRegressionTests
{
    // A dummy closed handler is required alongside the open-generic ones: Execute only emits any
    // generated file at all when handlerInfos.Count > 0 (see GenerateHandlerRegistrationMethod's
    // caller), and this scenario is otherwise open-generic-only.
    private const string DifferentOpenGenericShapesSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.OpenGenericRegression;

        public record PingQuery(string Value) : IKyrolusQuery<string>;
        public class PingQueryHandler : IKyrolusQueryHandler<PingQuery, string>
        {
            public Task<string> Handle(PingQuery request, CancellationToken cancellationToken) => Task.FromResult(request.Value);
        }

        // Two open-generic command handlers implementing the same handler interface family
        // (IKyrolusCommandHandler<,>) but closing over two different open request shapes - the
        // exact scenario from the reflection package's confirmed duplicate-detection bug, mirrored
        // here for the generator's own SMG004 diagnostic and DI registration.
        public record CreateCommand<T>(T Payload) : IKyrolusCommand<T>;
        public record UpdateCommand<T>(T Payload) : IKyrolusCommand<T>;

        public class CreateHandler<T> : IKyrolusCommandHandler<CreateCommand<T>, T>
        {
            public Task<T> Handle(CreateCommand<T> request, CancellationToken cancellationToken) => Task.FromResult(request.Payload);
        }

        public class UpdateHandler<T> : IKyrolusCommandHandler<UpdateCommand<T>, T>
        {
            public Task<T> Handle(UpdateCommand<T> request, CancellationToken cancellationToken) => Task.FromResult(request.Payload);
        }
        """;

    [Fact(DisplayName = "Fix 1: two open generic handlers closing over different open request shapes do not report SMG004")]
    public void DifferentOpenGenericShapes_DoNotReportSMG004()
    {
        var result = GeneratorTestHost.Run(DifferentOpenGenericShapesSource);

        result.GeneratorDiagnostics.ShouldNotContain(d => d.Id == "SMG004");
        result.CompilationErrors.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Fix 2: both open generic handlers get their own TryAddEnumerable registration, not just the first")]
    public void DifferentOpenGenericShapes_BothHandlersRegistered()
    {
        var result = GeneratorTestHost.Run(DifferentOpenGenericShapesSource);
        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];

        // Neither line may use TryAddTransient: it dedupes on ServiceType alone, so the second
        // registration for the same open IKyrolusCommandHandler<,> would silently be dropped.
        handlersDiCode.ShouldContain(
            "services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusCommandHandler<,>), typeof(global::MyApp.OpenGenericRegression.CreateHandler<>)));");
        handlersDiCode.ShouldContain(
            "services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusCommandHandler<,>), typeof(global::MyApp.OpenGenericRegression.UpdateHandler<>)));");
    }

    [Fact(DisplayName = "Fix 2: both open generic handlers survive AddKyrolusMediatorHandlers as distinct service registrations")]
    public void DifferentOpenGenericShapes_BothHandlers_SurviveRegistration()
    {
        var result = GeneratorTestHost.Run(DifferentOpenGenericShapesSource);
        result.CompilationErrors.ShouldBeEmpty();

        // Emit the generator's output plus the original source to a real in-memory assembly and load
        // it, then actually execute the generated AddKyrolusMediatorHandlers() - so this proves the
        // fix by running the emitted code, not just by pattern-matching its text.
        //
        // This stops short of serviceProvider.GetService(...): Microsoft.Extensions.DependencyInjection's
        // built-in container additionally requires an open-generic service type and its open-generic
        // implementation type to have the *same* generic arity - IKyrolusCommandHandler<,> is arity 2,
        // CreateHandler<> is arity 1 - and throws "Arity of open generic service type ... does not
        // equal arity of open generic implementation type ..." out of BuildServiceProvider() for this
        // shape, regardless of TryAddTransient vs TryAddEnumerable. That is a separate, pre-existing
        // limitation of the stock container for this "wraps the handler's own parameter" pattern, not
        // something Fix 2 changed or could change - so this test verifies what Fix 2 actually affects:
        // whether both registrations exist in the IServiceCollection at all.
        using var peStream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(peStream);
        emitResult.Success.ShouldBeTrue(
            string.Join("; ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())));
        var assembly = Assembly.Load(peStream.ToArray());

        var services = new ServiceCollection();

        var diExtensionsType = assembly.GetType("Microsoft.Extensions.DependencyInjection.KyrolusMediatorGeneratedHandlersDIExtensions")
            ?? throw new InvalidOperationException("Generated DI extensions type not found in emitted assembly.");
        var addHandlersMethod = diExtensionsType.GetMethod("AddKyrolusMediatorHandlers")
            ?? throw new InvalidOperationException("AddKyrolusMediatorHandlers not found.");
        addHandlersMethod.Invoke(null, [services]);

        var createHandlerType = assembly.GetType("MyApp.OpenGenericRegression.CreateHandler`1")
            ?? throw new InvalidOperationException("CreateHandler`1 not found in emitted assembly.");
        var updateHandlerType = assembly.GetType("MyApp.OpenGenericRegression.UpdateHandler`1")
            ?? throw new InvalidOperationException("UpdateHandler`1 not found in emitted assembly.");
        var commandHandlerServiceType = typeof(KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusCommandHandler<,>);

        // Before the fix, TryAddTransient(typeof(IKyrolusCommandHandler<,>), typeof(...)) deduped on
        // ServiceType alone, so the second call for the same open interface was silently a no-op and
        // only one of these two descriptors would exist.
        services.Count(d => d.ServiceType == commandHandlerServiceType && d.ImplementationType == createHandlerType).ShouldBe(1);
        services.Count(d => d.ServiceType == commandHandlerServiceType && d.ImplementationType == updateHandlerType).ShouldBe(1);
    }

    private const string OrderedBehaviorsSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;
        using KyrolusSous.Mediator.Runtime.Config;

        namespace MyApp.OrderedBehaviors;

        public record OrderedQuery(int Id) : IKyrolusQuery<string>;
        public class OrderedQueryHandler : IKyrolusQueryHandler<OrderedQuery, string>
        {
            public Task<string> Handle(OrderedQuery request, CancellationToken cancellationToken) => Task.FromResult("ok");
        }

        // Named so the OLD alphabetical sort and the call-site order disagree on which comes first:
        // "ZBehavior" is registered first here but would sort after "ABehavior" alphabetically.
        public class ZBehavior<TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>
        {
            public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
                => next(cancellationToken);
        }

        public class ABehavior<TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>
        {
            public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
                => next(cancellationToken);
        }

        public static class Setup
        {
            public static void Configure(KyrolusMediatorConfiguration configuration)
            {
                configuration.AddOpenBehavior(typeof(ZBehavior<,>));
                configuration.AddOpenBehavior(typeof(ABehavior<,>));
            }
        }
        """;

    [Fact(DisplayName = "Fix 3: two open behaviors sharing a (request, response) pair keep call-site order, not alphabetical order")]
    public void AddOpenBehavior_MultipleBehaviors_PreserveCallSiteOrder()
    {
        var result = GeneratorTestHost.Run(OrderedBehaviorsSource);

        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];

        var zIndex = handlersDiCode.IndexOf(
            "global::MyApp.OrderedBehaviors.ZBehavior<global::MyApp.OrderedBehaviors.OrderedQuery, string>", StringComparison.Ordinal);
        var aIndex = handlersDiCode.IndexOf(
            "global::MyApp.OrderedBehaviors.ABehavior<global::MyApp.OrderedBehaviors.OrderedQuery, string>", StringComparison.Ordinal);

        zIndex.ShouldBeGreaterThanOrEqualTo(0);
        aIndex.ShouldBeGreaterThanOrEqualTo(0);
        zIndex.ShouldBeLessThan(aIndex,
            "ZBehavior was passed to AddOpenBehavior before ABehavior, so the closed registrations " +
            "must preserve that call-site order rather than alphabetically re-sorting them - " +
            "otherwise two behaviors sharing a PipelineOrder value would run in a different relative " +
            "order under the generator (AOT) than under the reflection/JIT path for the same source.");
    }
}
