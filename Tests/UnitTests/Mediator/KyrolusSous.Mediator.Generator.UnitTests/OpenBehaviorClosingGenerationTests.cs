using KyrolusSous.Mediator.Generator;
using Shouldly;

namespace KyrolusSous.Mediator.Generator.UnitTests;

/// <summary>
/// Covers <c>AddOpenBehavior(typeof(...))</c> closing: the generator should emit an already-closed
/// registration for every (request, response) pair a user-supplied open-generic behavior's own type
/// parameter constraints actually allow, guarded so it only takes effect where the runtime declines
/// (no NativeAOT support). See AppendClosedUserOpenBehaviors / ComputeClosedUserOpenBehaviorRegistrations.
/// </summary>
public sealed class OpenBehaviorClosingGenerationTests
{
    private const string ConstrainedBehaviorSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;
        using KyrolusSous.Mediator.Runtime.Config;

        namespace MyApp.OpenBehaviors;

        public interface ICacheable { }

        public record CacheableQuery(int Id) : IKyrolusQuery<string>, ICacheable;
        public class CacheableQueryHandler : IKyrolusQueryHandler<CacheableQuery, string>
        {
            public Task<string> Handle(CacheableQuery request, CancellationToken cancellationToken) => Task.FromResult("ok");
        }

        public record PlainQuery(int Id) : IKyrolusQuery<string>;
        public class PlainQueryHandler : IKyrolusQueryHandler<PlainQuery, string>
        {
            public Task<string> Handle(PlainQuery request, CancellationToken cancellationToken) => Task.FromResult("ok");
        }

        // Deliberately swaps declaration order (TResponse first) relative to how it plugs into the
        // interface, to prove the generator reads the mapping from the interface's own type
        // arguments rather than assuming the behavior declares TRequest first.
        public class CachingBehavior<TResponse, TRequest> : IKyrolusPipelineBehavior<TRequest, TResponse>
            where TRequest : ICacheable
        {
            public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
                => next(cancellationToken);
        }

        public static class Setup
        {
            public static void Configure(KyrolusMediatorConfiguration configuration)
            {
                configuration.AddOpenBehavior(typeof(CachingBehavior<,>));
            }
        }
        """;

    [Fact(DisplayName = "AddOpenBehavior(typeof(...)): a constrained open behavior is closed only for request/response pairs that satisfy its constraints")]
    public void AddOpenBehavior_ClosesOnlyConstraintSatisfyingPairs()
    {
        // Act
        var result = GeneratorTestHost.Run(ConstrainedBehaviorSource);

        // Assert: most importantly, what the generator emitted actually compiles - a wrong
        // constraint check would emit CachingBehavior<..PlainQuery, string> here, which does not.
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];

        handlersDiCode.ShouldContain("RuntimeFeature.IsDynamicCodeSupported");
        // CachingBehavior<TResponse, TRequest> declares TResponse first, so the closed form must
        // put "string" (the response) first too - not "request first" regardless of declaration order.
        handlersDiCode.ShouldContain(
            "services.TryAddEnumerable(ServiceDescriptor.Transient<global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusPipelineBehavior<global::MyApp.OpenBehaviors.CacheableQuery, string>, global::MyApp.OpenBehaviors.CachingBehavior<string, global::MyApp.OpenBehaviors.CacheableQuery>>());");

        // PlainQuery does not implement ICacheable, so the constrained behavior must not be closed
        // over it at all.
        handlersDiCode.ShouldNotContain("CachingBehavior<global::MyApp.OpenBehaviors.PlainQuery");
    }

    private const string StreamBehaviorSource = """
        using System.Collections.Generic;
        using System.Threading;
        using KyrolusSous.Mediator.Abstractions.Interfaces;
        using KyrolusSous.Mediator.Runtime.Config;

        namespace MyApp.OpenBehaviors;

        public record CountStream(int Upto) : IKyrolusStreamRequest<int>;
        public class CountStreamHandler : IKyrolusStreamRequestHandler<CountStream, int>
        {
            public async IAsyncEnumerable<int> Handle(CountStream request, CancellationToken cancellationToken)
            {
                for (var i = 0; i < request.Upto; i++)
                {
                    yield return i;
                }
                await System.Threading.Tasks.Task.CompletedTask;
            }
        }

        public class LoggingStreamBehavior<TRequest, TResponse> : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
        {
            public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
                => next(cancellationToken);
        }

        public static class Setup
        {
            public static void Configure(KyrolusMediatorConfiguration configuration)
            {
                configuration.AddOpenBehavior(typeof(LoggingStreamBehavior<,>));
            }
        }
        """;

    [Fact(DisplayName = "AddOpenBehavior(typeof(...)): an unconstrained open stream behavior is closed against every stream (request, response) pair")]
    public void AddOpenBehavior_ClosesStreamBehavior()
    {
        // Act
        var result = GeneratorTestHost.Run(StreamBehaviorSource);

        // Assert
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];

        handlersDiCode.ShouldContain(
            "services.TryAddEnumerable(ServiceDescriptor.Transient<global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusStreamPipelineBehavior<global::MyApp.OpenBehaviors.CountStream, int>, global::MyApp.OpenBehaviors.LoggingStreamBehavior<global::MyApp.OpenBehaviors.CountStream, int>>());");
    }

    private const string NoOpenBehaviorSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.NoOpenBehaviors;

        public record PlainQuery(int Id) : IKyrolusQuery<string>;
        public class PlainQueryHandler : IKyrolusQueryHandler<PlainQuery, string>
        {
            public Task<string> Handle(PlainQuery request, CancellationToken cancellationToken) => Task.FromResult("ok");
        }
        """;

    [Fact(DisplayName = "No AddOpenBehavior call: nothing extra is emitted for user open behaviors")]
    public void NoAddOpenBehaviorCall_EmitsNothingExtra()
    {
        // Act
        var result = GeneratorTestHost.Run(NoOpenBehaviorSource);

        // Assert
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];

        // The built-in four still get their own guarded block (a query handler exists here), but
        // AppendClosedUserOpenBehaviors must have emitted nothing at all - not even an empty guard.
        handlersDiCode.ShouldNotContain("User-supplied open-generic behaviors");
    }
}
