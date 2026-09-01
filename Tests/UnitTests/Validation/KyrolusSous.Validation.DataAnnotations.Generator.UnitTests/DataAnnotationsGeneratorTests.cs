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
    [Fact(DisplayName = "Generator Generates Validator Class For Annotated Dto")]
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

    [Fact(DisplayName = "Generator handles a RegularExpression pattern containing backslashes without breaking compilation")]
    public void Generator_HandlesBackslashesInRegexPattern()
    {
        var source = @"
using System.ComponentModel.DataAnnotations;

namespace MyTestApp;

public class PhoneDto
{
    [RegularExpression(@""^\d{3}-\d{4}$"")]
    public string Phone { get; set; } = string.Empty;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact(DisplayName = "Generator handles a custom ErrorMessage containing a double quote without breaking compilation")]
    public void Generator_HandlesQuoteInCustomErrorMessage()
    {
        var source = @"
using System.ComponentModel.DataAnnotations;

namespace MyTestApp;

public class QuotedMessageDto
{
    [Required(ErrorMessage = ""Field \""Name\"" is required"")]
    public string Name { get; set; } = string.Empty;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact(DisplayName = "Generator emits real checks for Phone, CreditCard, and Url attributes and compiles cleanly")]
    public void Generator_GeneratesChecksFor_Phone_CreditCard_Url()
    {
        var source = @"
using System.ComponentModel.DataAnnotations;

namespace MyTestApp;

public class ContactDto
{
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [CreditCard]
    public string Card { get; set; } = string.Empty;

    [Url]
    public string Website { get; set; } = string.Empty;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var allGeneratedCode = string.Join("\n", outputCompilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        allGeneratedCode.ShouldContain("IsValidCreditCardNumber");
        allGeneratedCode.ShouldContain("StartsWith(\"http://\"");
        allGeneratedCode.ShouldContain("digitCount_PhoneNumber");
    }

    [Fact(DisplayName = "Generator bakes RuleSets/Groups from [KyrolusValidationScope] into the generated failure and implements the context-aware interface")]
    public void Generator_TagsFailuresWithRuleSetsAndGroups_FromScopeAttribute()
    {
        var source = @"
using System.ComponentModel.DataAnnotations;
using KyrolusSous.Validation.Abstractions;

namespace MyTestApp;

public class CreateUserDto
{
    [Required, MinLength(8)]
    [KyrolusValidationScope(RuleSets = new[] { ""Create"" })]
    public string Password { get; set; } = string.Empty;

    [Required]
    [KyrolusValidationScope(Groups = new[] { ""Audit"" })]
    public string CreatedBy { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var allGeneratedCode = string.Join("\n", outputCompilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        allGeneratedCode.ShouldContain("IKyrolusRequestValidatorWithContext<");
        allGeneratedCode.ShouldContain("new[] { \"Create\" }");
        allGeneratedCode.ShouldContain("new[] { \"Audit\" }");
        allGeneratedCode.ShouldContain("KyrolusValidationScopeResolver.ResolveActiveRuleSet");
        // The untagged Name property must keep passing Array.Empty<string>() for both dimensions.
        allGeneratedCode.ShouldContain("AddFailure(failures, \"Name\", ");
    }

    [Fact(DisplayName = "Generator reports a diagnostic for an unsupported custom ValidationAttribute instead of silently skipping it")]
    public void Generator_ReportsDiagnostic_ForUnsupportedAttribute()
    {
        var source = @"
using System;
using System.ComponentModel.DataAnnotations;

namespace MyTestApp;

public sealed class AlwaysValidAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context) => ValidationResult.Success;
}

public class CustomAttributeDto
{
    [AlwaysValid]
    public string Custom { get; set; } = string.Empty;
}
";

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.ShouldContain(d => d.Id == "KYVALGEN001");
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, Compilation OutputCompilation) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RequiredAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IKyrolusRequestValidator<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Text.RegularExpressions").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel").Location)
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
