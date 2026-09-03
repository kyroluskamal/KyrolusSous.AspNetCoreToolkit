using KyrolusSous.Mediator.Generator;
using Microsoft.CodeAnalysis;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class EdgeCasesGenerationTests
{
    private const string OpenGenericAndAbstractSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.EdgeCases;

        public record SpecificQuery(int Id) : IKyrolusQuery<string>;

        // 1. Abstract Handler (Should be completely ignored by Generator)
        public abstract class BaseQueryHandler<TQuery, TResponse> : IKyrolusQueryHandler<TQuery, TResponse>
            where TQuery : IKyrolusQuery<TResponse>
        {
            public abstract Task<TResponse> Handle(TQuery request, CancellationToken cancellationToken);
        }

        // 2. Concrete implementation of abstract handler
        public class ConcreteQueryHandler : BaseQueryHandler<SpecificQuery, string>
        {
            public override Task<string> Handle(SpecificQuery request, CancellationToken cancellationToken)
                => Task.FromResult("Concrete");
        }

        // 3. Open Generic Handler
        public class GenericLoggingHandler<TRequest, TResponse> : IKyrolusRequestHandler<TRequest, TResponse>
            where TRequest : IKyrolusRequest<TResponse>
        {
            public Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
                => Task.FromResult<TResponse>(default!);
        }

        // 4. Plain Class with BaseList that does not implement any Mediator interface
        public class UnrelatedClass : System.IDisposable
        {
            public void Dispose() {}
        }
        """;

    [Fact(DisplayName = "Edge Cases: Abstract classes ignored, open generic handlers registered, unrelated classes ignored")]
    public void EdgeCases_AbstractClassesAndUnrelatedClassesHandledCorrectly()
    {
        // Act
        var result = GeneratorTestHost.Run(OpenGenericAndAbstractSource);

        // Assert
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];
        
        // Abstract class must NOT be registered in DI
        handlersDiCode.ShouldNotContain("BaseQueryHandler");

        // Concrete class MUST be registered in DI
        handlersDiCode.ShouldContain("services.TryAddTransient<global::MyApp.EdgeCases.ConcreteQueryHandler>()");

        // Open Generic Handler registered with TryAddEnumerable(ServiceDescriptor.Transient(typeof(...), typeof(...)))
        // - not TryAddTransient, which dedupes on ServiceType alone and would silently drop a second,
        // legitimately different open-generic handler registered against the same open interface.
        handlersDiCode.ShouldContain("services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusRequestHandler<,>), typeof(global::MyApp.EdgeCases.GenericLoggingHandler<,>)))");

        // Unrelated class must NOT be registered
        handlersDiCode.ShouldNotContain("UnrelatedClass");
    }

    [Fact(DisplayName = "Edge Cases: the open-generic reflection fallback unwraps TargetInvocationException instead of invoking the handler's Handle method raw")]
    public void EdgeCases_OpenGenericFallback_UnwrapsTargetInvocationException()
    {
        // Act
        var result = GeneratorTestHost.Run(OpenGenericAndAbstractSource);

        // Assert
        result.CompilationErrors.ShouldBeEmpty();

        var dispatcherCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedDispatcher.g.cs"];

        // The shared unwrap-and-rethrow helper is emitted once...
        dispatcherCode.ShouldContain("private static object? InvokeHandlerMethod(global::System.Reflection.MethodInfo method, object target, object?[] arguments)");
        dispatcherCode.ShouldContain("global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();");

        // ...and every open-generic fallback call site goes through it instead of calling
        // MethodInfo.Invoke directly, which would otherwise surface a TargetInvocationException to
        // the caller instead of the handler's real exception type.
        dispatcherCode.ShouldNotContain("method.Invoke(commandHandler");
        dispatcherCode.ShouldNotContain("method.Invoke(openHandler");
        dispatcherCode.ShouldContain("InvokeHandlerMethod(method, commandHandler");
        dispatcherCode.ShouldContain("InvokeHandlerMethod(method, openHandler");
    }

    [Fact(DisplayName = "Edge Cases: Missing Abstractions assembly should report SMG001 diagnostic")]
    public void MissingAbstractions_ShouldReportDiagnosticSMG001()
    {
        const string PlainClassCode = "public class Dummy {}";

        // Act
        var result = GeneratorTestHost.RunWithoutAbstractions(PlainClassCode);

        // Assert: Generator reports SMG001 diagnostic error
        result.GeneratorDiagnostics.ShouldNotBeEmpty();
        result.GeneratorDiagnostics.ShouldContain(d => d.Id == "SMG001");
    }

    private const string DuplicateHandlerSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Duplicates;

        public record DupQuery(int Id) : IKyrolusQuery<string>;

        public class DupQueryHandlerOne : IKyrolusQueryHandler<DupQuery, string>
        {
            public Task<string> Handle(DupQuery request, CancellationToken cancellationToken) => Task.FromResult("one");
        }

        public class DupQueryHandlerTwo : IKyrolusQueryHandler<DupQuery, string>
        {
            public Task<string> Handle(DupQuery request, CancellationToken cancellationToken) => Task.FromResult("two");
        }
        """;

    [Fact(DisplayName = "Edge Cases: Two concrete handlers for the same (request, response) pair report SMG003")]
    public void DuplicateConcreteHandlers_ShouldReportDiagnosticSMG003()
    {
        // Act
        var result = GeneratorTestHost.Run(DuplicateHandlerSource);

        // Assert: reported as a build-breaking error, naming both handlers - not a silent
        // last-one-wins entry in the dispatch table.
        result.GeneratorDiagnostics.ShouldContain(d => d.Id == "SMG003" && d.Severity == DiagnosticSeverity.Error);
        var message = result.GeneratorDiagnostics.Single(d => d.Id == "SMG003").GetMessage();
        message.ShouldContain("DupQueryHandlerOne");
        message.ShouldContain("DupQueryHandlerTwo");
    }

    private const string DuplicateOpenGenericHandlerSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Duplicates;

        public class OpenHandlerOne<TRequest, TResponse> : IKyrolusRequestHandler<TRequest, TResponse>
            where TRequest : IKyrolusRequest<TResponse>
        {
            public Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken) => Task.FromResult<TResponse>(default!);
        }

        public class OpenHandlerTwo<TRequest, TResponse> : IKyrolusRequestHandler<TRequest, TResponse>
            where TRequest : IKyrolusRequest<TResponse>
        {
            public Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken) => Task.FromResult<TResponse>(default!);
        }
        """;

    [Fact(DisplayName = "Edge Cases: Two open generic handlers for the same interface report SMG004")]
    public void DuplicateOpenGenericHandlers_ShouldReportDiagnosticSMG004()
    {
        // Act
        var result = GeneratorTestHost.Run(DuplicateOpenGenericHandlerSource);

        // Assert
        result.GeneratorDiagnostics.ShouldContain(d => d.Id == "SMG004" && d.Severity == DiagnosticSeverity.Error);
        var message = result.GeneratorDiagnostics.Single(d => d.Id == "SMG004").GetMessage();
        message.ShouldContain("OpenHandlerOne");
        message.ShouldContain("OpenHandlerTwo");
    }
}
