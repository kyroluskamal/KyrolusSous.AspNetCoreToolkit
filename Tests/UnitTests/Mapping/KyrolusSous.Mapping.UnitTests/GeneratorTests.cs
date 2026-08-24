namespace KyrolusSous.Mapping.UnitTests;

public sealed class GeneratorTests
{
    [Fact(DisplayName = "KyrolusMappingGenerator: Emits direct assignment extension methods from [KyrolusMapTo] attribute")]
    public void Generator_EmitsDirectAssignments_ForMapTo()
    {
        var sourceCode = """
            using KyrolusSous.Mapping.Abstractions.Attributes;

            namespace TestApp;

            [KyrolusMapTo(typeof(CustomerDto))]
            public class Customer
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
                public decimal Balance { get; set; }
            }

            public class CustomerDto
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
                public decimal Balance { get; set; }
            }
            """;

        var (diagnostics, output) = RunGenerator(sourceCode);

        diagnostics.ShouldBeEmpty();
        output.ShouldContain("public static global::TestApp.CustomerDto? ToCustomerDto(this global::TestApp.Customer? source)");
        output.ShouldContain("target.Id = source.Id;");
        output.ShouldContain("target.Name = source.Name;");
        output.ShouldContain("target.Balance = source.Balance;");
    }

    [Fact(DisplayName = "KyrolusMappingGenerator: Emits bidirectional mapping methods when IsBidirectional is enabled")]
    public void Generator_EmitsBidirectionalMapping_WhenEnabled()
    {
        var sourceCode = """
            using KyrolusSous.Mapping.Abstractions.Attributes;

            namespace TestApp;

            [KyrolusMapTo(typeof(UserDto), IsBidirectional = true)]
            public class User
            {
                public int Id { get; set; }
                public string Email { get; set; } = string.Empty;
            }

            public class UserDto
            {
                public int Id { get; set; }
                public string Email { get; set; } = string.Empty;
            }
            """;

        var (diagnostics, output) = RunGenerator(sourceCode);

        diagnostics.ShouldBeEmpty();
        output.ShouldContain("public static global::TestApp.UserDto? ToUserDto(this global::TestApp.User? source)");
        output.ShouldContain("public static global::TestApp.User? ToUser(this global::TestApp.UserDto? source)");
    }

    [Fact(DisplayName = "KyrolusMappingGenerator: Emits positional constructor bindings with extra properties")]
    public void Generator_EmitsPositionalConstructorBindings_WithExtraProperties()
    {
        var sourceCode = """
            using KyrolusSous.Mapping.Abstractions.Attributes;

            namespace TestApp;

            [KyrolusMapTo(typeof(OrderDto), true)]
            public class Order
            {
                public int OrderId { get; set; }
                public decimal Total { get; set; }
                public string Status { get; set; } = string.Empty;
                
                [KyrolusIgnoreMap]
                public string InternalSecret { get; set; } = string.Empty;
            }

            public record OrderDto(int OrderId, decimal Total)
            {
                public string Status { get; set; } = string.Empty;

                [KyrolusIgnoreMap]
                public string IgnoredField { get; set; } = string.Empty;
            }
            """;

        var (diagnostics, output) = RunGenerator(sourceCode);

        diagnostics.ShouldBeEmpty();
        output.ShouldContain("public static global::TestApp.OrderDto? ToOrderDto(this global::TestApp.Order? source)");
        output.ShouldContain("var target = new global::TestApp.OrderDto(source.OrderId, source.Total);");
        output.ShouldContain("target.Status = source.Status;");
        output.ShouldNotContain("InternalSecret");
        output.ShouldNotContain("IgnoredField");
    }

    [Fact(DisplayName = "KyrolusMappingGenerator: Emits partial mapper classes decorated with [KyrolusMapper]")]
    public void Generator_EmitsPartialMapperClasses()
    {
        var sourceCode = """
            using KyrolusSous.Mapping.Abstractions.Attributes;

            namespace TestApp;

            public class Product
            {
                public int Id { get; set; }
                public string Title { get; set; } = string.Empty;
            }

            public class ProductDto
            {
                public int Id { get; set; }
                
                [KyrolusMapProperty("Title")]
                public string Title { get; set; } = string.Empty;
            }

            public record ProductRecord(int Id)
            {
                public string Title { get; set; } = string.Empty;
            }

            [KyrolusMapper]
            public partial class ProductMapper
            {
                public partial ProductDto Map(Product source);
                public partial void MapInPlace(Product source, ProductDto target);
                public partial ProductRecord MapToRecord(Product source);
            }
            """;

        var (diagnostics, output) = RunGenerator(sourceCode);

        diagnostics.ShouldBeEmpty();
        output.ShouldContain("public partial class ProductMapper");
        output.ShouldContain("public partial global::TestApp.ProductDto Map(global::TestApp.Product source)");
        output.ShouldContain("public partial void MapInPlace(global::TestApp.Product source, global::TestApp.ProductDto target)");
        output.ShouldContain("public partial global::TestApp.ProductRecord MapToRecord(global::TestApp.Product source)");
        output.ShouldContain("var target = new global::TestApp.ProductRecord(source.Id);");
    }

    [Fact(DisplayName = "KyrolusMappingGenerator: Emits mapping when using [KyrolusMapFrom] attribute")]
    public void Generator_EmitsMapping_ForMapFrom()
    {
        var sourceCode = """
            using KyrolusSous.Mapping.Abstractions.Attributes;

            namespace TestApp;

            public class Item
            {
                public int Id { get; set; }
                public string Code { get; set; } = string.Empty;
            }

            [KyrolusMapFrom(typeof(Item), isBidirectional: true)]
            public class ItemDto
            {
                public int Id { get; set; }
                public string Code { get; set; } = string.Empty;
            }
            """;

        var (diagnostics, output) = RunGenerator(sourceCode);

        diagnostics.ShouldBeEmpty();
        output.ShouldContain("public static global::TestApp.ItemDto? ToItemDto(this global::TestApp.Item? source)");
        output.ShouldContain("public static global::TestApp.Item? ToItem(this global::TestApp.ItemDto? source)");
    }

    private static (IReadOnlyList<Diagnostic> Diagnostics, string GeneratedSource) RunGenerator(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .Append(MetadataReference.CreateFromFile(typeof(KyrolusMapToAttribute).Assembly.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new KyrolusMappingGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSource = string.Join("\n", runResult.GeneratedTrees.Select(t => t.ToString()));

        return (diagnostics, generatedSource);
    }
}
