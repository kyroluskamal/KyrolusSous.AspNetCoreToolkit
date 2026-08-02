using KyrolusSous.Mediator.Generator;
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

        // Open Generic Handler registered with TryAddTransient(typeof(...), typeof(...))
        handlersDiCode.ShouldContain("services.TryAddTransient(typeof(global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusRequestHandler<,>), typeof(global::MyApp.EdgeCases.GenericLoggingHandler<,>))");

        // Unrelated class must NOT be registered
        handlersDiCode.ShouldNotContain("UnrelatedClass");
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
}
