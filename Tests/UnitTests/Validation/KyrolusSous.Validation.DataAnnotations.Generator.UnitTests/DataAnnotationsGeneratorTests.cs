using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using KyrolusSous.Validation.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace KyrolusSous.Validation.DataAnnotations.Generator.UnitTests;

public sealed class DataAnnotationsGeneratorTests
{
    [Fact]
    public void Generator_GeneratesValidatorClassForAnnotatedDto()
    {
        var source = @"
using System.ComponentModel.DataAnnotations;

namespace MyTestApp;

public class CreateUserDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Range(18, 100)]
    public int Age { get; set; }

    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source);

        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBeGreaterThan(0);

        var allGeneratedCode = string.Join("\n", generatedTrees.Select(t => t.ToString()));
        allGeneratedCode.ShouldContain("CreateUserDtoGeneratedDataAnnotationsValidator");
        allGeneratedCode.ShouldContain("The Name field is required.");
        allGeneratedCode.ShouldContain("The field Name must be a string with a minimum length of 3 and maximum length of 50.");
        allGeneratedCode.ShouldContain("The field Age must be between 18 and 100.");
        allGeneratedCode.ShouldContain("The Email field is not a valid e-mail address.");
        allGeneratedCode.ShouldContain("AddKyrolusGeneratedDataAnnotationsValidators");
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, Compilation OutputCompilation) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RequiredAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IKyrolusRequestValidator<>).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new KyrolusDataAnnotationsValidationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        _ = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return (diagnostics, outputCompilation);
    }
}
