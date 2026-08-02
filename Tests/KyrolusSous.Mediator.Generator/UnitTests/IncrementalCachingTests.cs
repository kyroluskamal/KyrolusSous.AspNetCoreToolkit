using KyrolusSous.Mediator.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using System.Linq;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class IncrementalCachingTests
{
    private const string InitialSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Caching;

        public record GetUserQuery(int Id) : IKyrolusQuery<string>;

        public class GetUserQueryHandler : IKyrolusQueryHandler<GetUserQuery, string>
        {
            public Task<string> Handle(GetUserQuery request, CancellationToken cancellationToken)
                => Task.FromResult("User_" + request.Id);
        }
        """;

    private const string UnrelatedClassSource = """
        namespace MyApp.Caching;

        public class UnrelatedHelper
        {
            public int Calculate() => 42;
        }
        """;

    private const string NewHandlerSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Caching;

        public record CreateUserCommand(string Name) : IKyrolusCommand;

        public class CreateUserCommandHandler : IKyrolusCommandHandler<CreateUserCommand>
        {
            public Task Handle(CreateUserCommand request, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
        """;

    [Fact(DisplayName = "Incremental Caching: Unrelated code changes should reuse cached generator outputs")]
    public void UnrelatedCodeChange_ShouldReuseCachedGeneratorPipelineOutput()
    {
        // 1. Initial run with step tracking ON
        var initialResult = GeneratorTestHost.Run(InitialSource, trackSteps: true);
        initialResult.GeneratorDiagnostics.ShouldBeEmpty();
        initialResult.CompilationErrors.ShouldBeEmpty();

        // 2. Add an unrelated class file to the compilation
        var syntaxTree2 = CSharpSyntaxTree.ParseText(UnrelatedClassSource);
        var updatedCompilation = initialResult.InputCompilation.AddSyntaxTrees(syntaxTree2);

        // 3. Re-run generator driver on updated compilation
        var updatedDriver = initialResult.Driver.RunGeneratorsAndUpdateCompilation(
            updatedCompilation,
            out var outputCompilation,
            out var diagnostics);

        // 4. Assert: Generator produced valid compilation
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // 5. Assert: Check tracked steps in run results for Cached/Unchanged step execution
        var runResult = updatedDriver.GetRunResult();
        runResult.Results.ShouldNotBeEmpty();

        foreach (var generatorResult in runResult.Results)
        {
            // Verify tracked output steps exist and outputs are reused/cached
            if (generatorResult.TrackedOutputSteps.Any())
            {
                var outputs = generatorResult.TrackedOutputSteps.SelectMany(kvp => kvp.Value).SelectMany(step => step.Outputs);
                outputs.ShouldContain(o => o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged);
            }
        }
    }

    [Fact(DisplayName = "Incremental Caching: Adding a new handler should trigger incremental generation and update code")]
    public void RelatedCodeChange_AddingNewHandler_ShouldUpdateGeneratedCode()
    {
        // 1. Initial run
        var initialResult = GeneratorTestHost.Run(InitialSource, trackSteps: true);

        // 2. Add a new handler file to the compilation
        var syntaxTree2 = CSharpSyntaxTree.ParseText(NewHandlerSource);
        var updatedCompilation = initialResult.InputCompilation.AddSyntaxTrees(syntaxTree2);

        // 3. Re-run generator driver
        var updatedDriver = initialResult.Driver.RunGeneratorsAndUpdateCompilation(
            updatedCompilation,
            out var outputCompilation,
            out var diagnostics);

        // 4. Inspect generated dispatcher code
        var dispatcherSource = updatedDriver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName == "KyrolusSous.Mediator.GeneratedDispatcher.g.cs");

        dispatcherSource.SourceText.ShouldNotBeNull();
        dispatcherSource.SourceText.ToString().ShouldContain("typeof(global::MyApp.Caching.CreateUserCommand)");
    }
}
