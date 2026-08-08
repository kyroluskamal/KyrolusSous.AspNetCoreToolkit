namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetRepositoryTests;

public partial class GetRepositoryTests
{
    [Fact(DisplayName = "GetRepository<T>() throws when repository type is not registered")]
    public void GetRepository_Generic_Unregistered_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        Should.Throw<InvalidOperationException>(() => uow.GetRepository<UnregisteredRepository>());
    }

    [Fact(DisplayName = "GetRepository<T>(name) returns null for unknown name")]
    public void GetRepository_ByName_Unknown_ReturnsNull()
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var repo = uow.GetRepository<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>("NoSuchRepositoryType");
        repo.ShouldBeNull();
    }

    [Fact(DisplayName = "GetRepository<T>(name) returns null when resolved type is not assignable to requested type")]
    public void GetRepository_ByName_NotAssignable_ReturnsNull()
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

        var repo = uow.GetRepository<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>(typeof(NamedProductRepository).Name);
        repo.ShouldBeNull();
    }
}
