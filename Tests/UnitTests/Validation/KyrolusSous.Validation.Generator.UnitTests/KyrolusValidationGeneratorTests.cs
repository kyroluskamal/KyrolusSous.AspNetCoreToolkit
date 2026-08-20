namespace KyrolusSous.Validation.Generator.UnitTests;

public sealed class KyrolusValidationGeneratorTests
{
    [Fact(DisplayName = "Generator generates DI registration for classes implementing IKyrolusRequestValidator")]
    public void Generator_GeneratesValidatorRegistrations_ForValidClass()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using KyrolusSous.Validation.Abstractions;

namespace MyTestApp;

public class MyRequest { }

public class MyRequestValidator : IKyrolusRequestValidator<MyRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(MyRequest request, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
    }
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: true);

        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(1);

        var generatedCode = generatedTrees[0].ToString();
        generatedCode.ShouldContain("AddKyrolusGeneratedValidators");
        generatedCode.ShouldContain("AddKyrolusGeneratedValidationProfiles");
        generatedCode.ShouldContain("MyRequestValidator");
        generatedCode.ShouldContain("IKyrolusRequestValidator");
    }

    [Fact(DisplayName = "Generator generates only validation profiles when no validators exist")]
    public void Generator_GeneratesOnlyProfiles_WhenNoValidatorsExist()
    {
        var source = @"
namespace MyTestApp;

public class MyRequest { }
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: true);

        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(1);

        var generatedCode = generatedTrees[0].ToString();
        generatedCode.ShouldNotContain("AddKyrolusGeneratedValidators");
        generatedCode.ShouldContain("AddKyrolusGeneratedValidationProfiles");
    }

    [Fact(DisplayName = "Generator returns early without generating code when Abstractions reference is missing")]
    public void Generator_ReturnsEarly_WhenAbstractionsMissing()
    {
        var source = @"
namespace MyTestApp;

public class MyRequest { }
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: false);

        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "Generator skips abstract and open-generic validator classes")]
    public void Generator_SkipsAbstractAndGenericValidators()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using KyrolusSous.Validation.Abstractions;

namespace MyTestApp;

public class MyRequest { }

public abstract class AbstractValidator<T> : IKyrolusRequestValidator<T>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(T request, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
    }
}

public class GenericValidator<T> : IKyrolusRequestValidator<T>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(T request, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
    }
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: true);

        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(1);

        var generatedCode = generatedTrees[0].ToString();
        generatedCode.ShouldNotContain("AddKyrolusGeneratedValidators");
        generatedCode.ShouldContain("AddKyrolusGeneratedValidationProfiles");
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, Compilation OutputCompilation) RunGenerator(string source, bool includeAbstractions)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var refList = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        if (includeAbstractions)
        {
            refList.Add(MetadataReference.CreateFromFile(typeof(IKyrolusRequestValidator<>).Assembly.Location));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            refList,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new KyrolusValidationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return (diagnostics, outputCompilation);
    }
}
