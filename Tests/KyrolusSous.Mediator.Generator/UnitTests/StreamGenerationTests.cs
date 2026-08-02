using KyrolusSous.Mediator.Generator;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Mediator.Generator.UnitTests;

public sealed class StreamGenerationTests
{
    private const string StreamSource = """
        using System;
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace MyApp.Streams;

        public record FetchNumbersStreamQuery(int Count) : IKyrolusStreamRequest<int>;

        public class FetchNumbersStreamQueryHandler : IKyrolusStreamRequestHandler<FetchNumbersStreamQuery, int>
        {
            public async IAsyncEnumerable<int> Handle(FetchNumbersStreamQuery request, [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                for (int i = 0; i < request.Count; i++)
                {
                    yield return i;
                }
                await Task.CompletedTask;
            }
        }
        """;

    [Fact(DisplayName = "Stream Generation should create DispatchStreamAsync and StreamPipelineWrapper")]
    public void StreamGeneration_ShouldGenerateStreamDispatcherAndWrapper()
    {
        // Act
        var result = GeneratorTestHost.Run(StreamSource);

        // Assert
        result.GeneratorDiagnostics.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();

        var dispatcherCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedDispatcher.g.cs"];
        dispatcherCode.ShouldContain("DispatchStreamAsync");
        dispatcherCode.ShouldContain("typeof(global::MyApp.Streams.FetchNumbersStreamQuery)");

        var wrapperCode = result.GeneratedSources["KyrolusSous.Mediator.GeneratedPipelineWrappers.g.cs"];
        wrapperCode.ShouldContain("CreateStream<global::MyApp.Streams.FetchNumbersStreamQuery, int>()");
    }
}
