using KyrolusSous.Mediator.Generator;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class QueryGenerationTests
{
    private const string QuerySource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp;

        public sealed class GetUserQuery : IKyrolusQuery<string>
        {
            public int UserId { get; set; }
        }

        public sealed class GetUserQueryHandler : IKyrolusQueryHandler<GetUserQuery, string>
        {
            public Task<string> Handle(GetUserQuery request, CancellationToken cancellationToken)
            {
                return Task.FromResult("User_" + request.UserId);
            }
        }
        """;

    [Fact(DisplayName = "Query Generation should create generated files and contain exact handler mappings")]
    public void QueryGeneration_ShouldCreateAllGeneratedFiles_WithExactMappings()
    {
        // 1. Arrange & Act
        var result = GeneratorTestHost.Run(QuerySource);

        // 2. Assert: No compilation errors or diagnostics
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();
        result.GeneratedSources.ShouldNotBeEmpty();

        // 3. Assert: Check presence of generated files
        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedDispatcher.g.cs");
        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedPipelineWrappers.g.cs");
        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs");

        // 4. Assert: Inspect exact generated code in Dispatcher
        var dispatcherCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedDispatcher.g.cs"];
        dispatcherCode.ShouldContain("typeof(global::MyApp.GetUserQuery)");
        dispatcherCode.ShouldContain("GetRequiredService<global::MyApp.GetUserQueryHandler>()");

        // 5. Assert: Inspect generated pipeline wrappers
        var wrapperCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedPipelineWrappers.g.cs"];
        wrapperCode.ShouldContain("typeof(global::MyApp.GetUserQuery)");
        wrapperCode.ShouldContain("typeof(string)");

        // 6. Assert: Inspect generated DI extension method
        var handlersDiCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedHandlersDIExtensions.g.cs"];
        handlersDiCode.ShouldContain("services.TryAddTransient<global::MyApp.GetUserQueryHandler>()");
    }
}
