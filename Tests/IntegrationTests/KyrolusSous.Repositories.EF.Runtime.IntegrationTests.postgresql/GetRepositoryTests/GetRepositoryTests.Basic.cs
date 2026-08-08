namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetRepositoryTests;

public partial class GetRepositoryTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record NameResolutionSpec(string ProvidedName);

    private static readonly IReadOnlyDictionary<string, NameResolutionSpec> NameResolutionSpecs = BuildNameResolutionSpecs();
    public static TheoryData<string> NameResolutionCases => CaseIdsFrom(NameResolutionSpecs);

    [Fact(DisplayName = "GetRepository<T>() returns cached scoped instance")]
    public void GetRepository_Generic_ReturnsCachedScopedInstance()
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var first = uow.GetRepository<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var second = uow.GetRepository<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        first.ShouldNotBeNull();
        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Theory(DisplayName = "GetRepository<T>(name) resolves by type name and full name")]
    [MemberData(nameof(NameResolutionCases))]
    public void GetRepository_ByName_ResolvesExpectedType(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = NameResolutionSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var byName = uow.GetRepository<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>(spec.ProvidedName);
        var byGeneric = uow.GetRepository<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        byName.ShouldNotBeNull();
        ReferenceEquals(byName, byGeneric).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetRepository<T>(name) with whitespace uses generic resolution")]
    public void GetRepository_ByName_Whitespace_UsesGenericResolution()
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var byWhitespace = uow.GetRepository<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>("   ");
        var byGeneric = uow.GetRepository<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        byWhitespace.ShouldNotBeNull();
        ReferenceEquals(byWhitespace, byGeneric).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetRepository<T>(name) resolves registered concrete repo by name")]
    public void GetRepository_ByName_ConcreteRegisteredType_ResolvesAssignable()
    {
        var customFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<NamedProductRepository>();
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var repo = uow.GetRepository<IKyrolusRepositoryAsync<ApplicationDbContext, Product, Guid>>(typeof(NamedProductRepository).Name.ToLowerInvariant());
        repo.ShouldNotBeNull();
        repo.ShouldBeOfType<NamedProductRepository>();
    }

    private static IReadOnlyDictionary<string, NameResolutionSpec> BuildNameResolutionSpecs()
    {
        var requestedType = typeof(KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>);
        return new Dictionary<string, NameResolutionSpec>
        {
            ["type-name"] = new(requestedType.Name),
            ["type-name-ignore-case"] = new(requestedType.Name.ToLowerInvariant()),
            ["full-name"] = new(requestedType.FullName!)
        };
    }
}
