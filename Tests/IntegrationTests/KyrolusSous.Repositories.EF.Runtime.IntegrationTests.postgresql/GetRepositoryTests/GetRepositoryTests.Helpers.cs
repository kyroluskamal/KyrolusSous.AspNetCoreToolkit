namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetRepositoryTests;

public partial class GetRepositoryTests
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected sealed class UnregisteredRepository;

    protected sealed class NamedProductRepository(ApplicationDbContext db)
        : KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>(db);
}
