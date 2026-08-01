using System.Collections.Immutable;
using System.Reflection;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Mediator.Generator.UnitTests;

/// <summary>
/// Runs the generator over source supplied as a string, in memory. No MSBuild, no files on disk.
/// </summary>
/// <remarks>
/// The four steps are always the same:
/// <list type="number">
/// <item><description>Parse the source into a syntax tree.</description></item>
/// <item><description>Build a <see cref="Compilation"/> from that tree plus metadata references.</description></item>
/// <item><description>Create a driver holding the generator.</description></item>
/// <item><description>Run it, and inspect what came out.</description></item>
/// </list>
/// This is also the easiest way to debug the generator: put a breakpoint in the generator and
/// debug a test. It is ordinary code in an ordinary process, so stepping just works.
/// </remarks>
internal static class GeneratorTestHost
{
    /// <summary>
    /// Every assembly currently loaded in the test process, as metadata references.
    /// </summary>
    /// <remarks>
    /// The in-memory compilation has to be able to resolve <c>IKyrolusQueryHandler</c> and the rest,
    /// or <c>GetTypeByMetadataName</c> returns null and the generator reports SMG001 instead of
    /// generating anything.
    /// <para>
    /// The assemblies that must be present are named through a type in each, not left to a scan of
    /// <see cref="AppDomain.CurrentDomain"/>. A scan alone is not enough: .NET loads assemblies
    /// lazily, so one that nothing has touched yet simply is not in the list, and the failure looks
    /// like the generator being broken rather than the harness missing a reference.
    /// </para>
    /// </remarks>
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        // Every framework assembly the host can see. Listing them one by one does not work in
        // practice: `IServiceProvider`, for instance, is type-forwarded to System.ComponentModel,
        // so referencing System.Private.CoreLib alone leaves the generated code failing to compile
        // for a reason that has nothing to do with the generator.
        var frameworkAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator);

        // Naming a type forces its assembly to load, which a bare `typeof(...)` discard does not
        // reliably do - the compiler can elide it.
        Assembly[] required =
        [
            typeof(IKyrolusQuery<>).Assembly,     // KyrolusSous.Mediator.Abstractions
            typeof(IServiceCollection).Assembly   // Microsoft.Extensions.DependencyInjection.Abstractions
        ];

        return frameworkAssemblies
            .Concat(required.Select(assembly => assembly.Location))
            .Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.Location))
            .Where(location => !string.IsNullOrWhiteSpace(location) && File.Exists(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToImmutableArray();
    }

    /// <summary>Runs the generator once over <paramref name="source"/>.</summary>
    /// <param name="source">C# source the generator should see, as text.</param>
    /// <param name="trackSteps">
    /// Enables step tracking, needed to assert on caching. Off by default because it costs memory
    /// and most tests do not need it.
    /// </param>
    public static GeneratorTestResult Run(string source, bool trackSteps = false)
    {
        // (1) Parse
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // (2) A compilation is a whole "project" as the compiler sees it
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // (3) The driver holds the generator and knows how to run it
        var driver = CSharpGeneratorDriver.Create(
            generators: [new MediatorGenerator().AsSourceGenerator()],
            additionalTexts: [],
            parseOptions: null,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: trackSteps));

        // (4) Run. `outputCompilation` is the original plus whatever the generator added.
        var resultDriver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        return new GeneratorTestResult(
            resultDriver,
            outputCompilation,
            generatorDiagnostics,
            compilation);
    }
}

/// <summary>What one generator run produced.</summary>
/// <param name="Driver">
/// The driver after the run. <c>Driver.GetRunResult()</c> exposes the generated trees and, when
/// tracking is on, <c>TrackedSteps</c>.
/// </param>
/// <param name="OutputCompilation">The input compilation plus the generated sources.</param>
/// <param name="GeneratorDiagnostics">Diagnostics the generator itself reported, such as SMG001.</param>
/// <param name="InputCompilation">The compilation before generation.</param>
internal sealed record GeneratorTestResult(
    GeneratorDriver Driver,
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    Compilation InputCompilation)
{
    /// <summary>The files the generator emitted, keyed by hint name.</summary>
    public IReadOnlyDictionary<string, string> GeneratedSources =>
        Driver.GetRunResult().Results
            .SelectMany(result => result.GeneratedSources)
            .ToDictionary(
                source => source.HintName,
                source => source.SourceText.ToString());

    /// <summary>
    /// Errors in the code after generation - which is the real question: does what the generator
    /// wrote actually compile?
    /// </summary>
    public ImmutableArray<Diagnostic> CompilationErrors =>
        [.. OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];
}
