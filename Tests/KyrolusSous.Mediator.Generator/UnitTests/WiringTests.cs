namespace KyrolusSous.Mediator.Generator.UnitTests;

/// <summary>
/// One test, whose only job is to prove the harness itself is wired correctly: the generator can
/// be constructed, the in-memory compilation resolves the abstractions, and the generator both
/// runs and emits code that compiles.
/// </summary>
/// <remarks>
/// If this fails, nothing else written against <see cref="GeneratorTestHost"/> can be trusted, so
/// start here when something looks wrong.
/// <para>
/// Real coverage of the generator's behaviour goes in other files alongside this one.
/// </para>
/// </remarks>
public sealed class WiringTests
{
    private const string OneHandler = """
        using System.Threading;
        using System.Threading.Tasks;
        using KyrolusSous.Mediator.Abstractions.Interfaces;

        namespace SampleApp;

        public record GetUser(int Id) : IKyrolusQuery<string>;

        public class GetUserHandler : IKyrolusQueryHandler<GetUser, string>
        {
            public Task<string> Handle(GetUser request, CancellationToken cancellationToken)
                => Task.FromResult($"user:{request.Id}");
        }
        """;

    [Fact]
    public void The_harness_runs_the_generator_and_its_output_compiles()
    {
        var result = GeneratorTestHost.Run(OneHandler);

        // The generator did not report a problem of its own - SMG001 here would mean the
        // in-memory compilation could not see the abstractions.
        result.GeneratorDiagnostics.ShouldBeEmpty();

        // It actually emitted something.
        result.GeneratedSources.ShouldNotBeEmpty();
        result.GeneratedSources.Keys.ShouldContain("KyrolusSous.Mediator.GeneratedDispatcher.g.cs");

        // And what it emitted is valid C# in the context it was added to. This is the assertion
        // that matters: a generator can happily emit code that does not compile.
        result.CompilationErrors.ShouldBeEmpty(
            "the generated code should compile; " +
            string.Join("; ", result.CompilationErrors.Select(d => d.ToString())));

        // The handler from the source above reached the dispatch table.
        result.GeneratedSources["KyrolusSous.Mediator.GeneratedDispatcher.g.cs"]
            .ShouldContain("SampleApp.GetUser");
    }
}
