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

    [Fact(DisplayName = "Generator returns early when no registrations and profiles type is not present")]
    public void Generator_ReturnsEarly_WhenNoRegistrationsAndNoProfiles()
    {
        var source = @"
namespace KyrolusSous.Validation.Abstractions
{
    public interface IKyrolusRequestValidator<TRequest> { }
}

namespace MyTestApp
{
    public class MyRequest { }
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: false);
        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "Generator generates validators without profiles when KyrolusValidationProfiles symbol is missing")]
    public void Generator_GeneratesOnlyValidators_WhenProfilesMissing()
    {
        var source = @"
namespace KyrolusSous.Validation.Abstractions
{
    public interface IKyrolusRequestValidator<TRequest> { }
}

namespace MyTestApp
{
    using KyrolusSous.Validation.Abstractions;

    public class MyRequest { }
    public class MyValidator : IKyrolusRequestValidator<MyRequest> { }
    public class UnrelatedClass : System.IDisposable
    {
        public void Dispose() { }
    }
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: false);
        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(1);

        var generatedCode = generatedTrees[0].ToString();
        generatedCode.ShouldContain("AddKyrolusGeneratedValidators");
        generatedCode.ShouldNotContain("using KyrolusSous.Validation.Abstractions;");
        generatedCode.ShouldNotContain("AddKyrolusGeneratedValidationProfiles");
    }

    [Fact(DisplayName = "Generator generates a hook order lookup for global hooks decorated with KyrolusValidationHookOrderAttribute")]
    public void Generator_GeneratesHookOrderLookup_ForOrderedGlobalHooks()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using KyrolusSous.Validation.Abstractions;

namespace MyTestApp;

[KyrolusValidationHookOrder(2)]
public class TracingHook : IKyrolusValidationHook
{
    public ValueTask OnBeforeAsync(object? request, KyrolusValidationContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnAfterAsync(object? request, KyrolusValidationContext context, IReadOnlyList<KyrolusValidationFailure> failures, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

[KyrolusValidationHookOrder(1)]
public class MetricsHook : IKyrolusValidationHook
{
    public ValueTask OnBeforeAsync(object? request, KyrolusValidationContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnAfterAsync(object? request, KyrolusValidationContext context, IReadOnlyList<KyrolusValidationFailure> failures, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: true);

        diagnostics.ShouldBeEmpty();

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(1);

        var generatedCode = generatedTrees[0].ToString();
        generatedCode.ShouldContain("AddKyrolusGeneratedValidationHookOrder");
        generatedCode.ShouldContain("KyrolusGeneratedValidationHookOrderLookup");
        generatedCode.ShouldContain("IKyrolusValidationHookOrderLookup");
        generatedCode.ShouldContain("TracingHook)) return 2;");
        generatedCode.ShouldContain("MetricsHook)) return 1;");
    }

    [Fact(DisplayName = "Generator generates a hook order lookup entry for IKyrolusValidationHook<TRequest> implementations too")]
    public void Generator_GeneratesHookOrderLookup_ForOrderedRequestSpecificHook()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using KyrolusSous.Validation.Abstractions;

namespace MyTestApp;

public class MyRequest { }

[KyrolusValidationHookOrder(3)]
public class MyRequestAuditHook : IKyrolusValidationHook<MyRequest>
{
    public ValueTask OnBeforeAsync(MyRequest request, KyrolusValidationContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnAfterAsync(MyRequest request, KyrolusValidationContext context, IReadOnlyList<KyrolusValidationFailure> failures, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: true);

        diagnostics.ShouldBeEmpty();

        var generatedCode = outputCompilation.SyntaxTrees.Skip(1).Single().ToString();
        generatedCode.ShouldContain("AddKyrolusGeneratedValidationHookOrder");
        generatedCode.ShouldContain("MyRequestAuditHook)) return 3;");
    }

    [Fact(DisplayName = "Generator does not generate a hook order lookup when no hook carries KyrolusValidationHookOrderAttribute")]
    public void Generator_DoesNotGenerateHookOrderLookup_WhenNoHookHasOrderAttribute()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using KyrolusSous.Validation.Abstractions;

namespace MyTestApp;

public class PlainHook : IKyrolusValidationHook
{
    public ValueTask OnBeforeAsync(object? request, KyrolusValidationContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask OnAfterAsync(object? request, KyrolusValidationContext context, IReadOnlyList<KyrolusValidationFailure> failures, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: true);

        diagnostics.ShouldBeEmpty();

        // Abstractions is referenced, so the profiles method alone still produces one generated file.
        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        generatedTrees.Count.ShouldBe(1);

        var generatedCode = generatedTrees[0].ToString();
        generatedCode.ShouldNotContain("AddKyrolusGeneratedValidationHookOrder");
        generatedCode.ShouldNotContain("KyrolusGeneratedValidationHookOrderLookup");
    }

    [Fact(DisplayName = "Generator ignores KyrolusValidationHookOrderAttribute on a class that isn't a validation hook")]
    public void Generator_IgnoresHookOrderAttribute_OnNonHookClass()
    {
        var source = @"
using KyrolusSous.Validation.Abstractions;

namespace MyTestApp;

[KyrolusValidationHookOrder(1)]
public class NotAHook { }
";

        var (diagnostics, outputCompilation) = RunGenerator(source, includeAbstractions: true);

        diagnostics.ShouldBeEmpty();

        var generatedCode = outputCompilation.SyntaxTrees.Skip(1).Single().ToString();
        generatedCode.ShouldNotContain("AddKyrolusGeneratedValidationHookOrder");
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
