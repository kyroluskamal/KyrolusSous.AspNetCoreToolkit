namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync does not return soft-deleted entities")]
    public async Task GetAllAsync_DoesNotReturnSoftDeletedEntities()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var UoW = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var product = await repo.GetByIdAsync(Guid.Parse("66666666-6666-6666-6666-666666666662"));

        product.ShouldNotBeNull();
        try
        {
            var x = await repo.SoftDeleteAsync(Guid.Parse("66666666-6666-6666-6666-666666666662"));
            var result = await UoW.SaveChangesAsync();
            x.ShouldBeTrue();
            result.ShouldBe(1);
            // Act
            var items = await repo.GetAllAsync(
                filter: null,
                orderBy: null,
                includeProperties: null,
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: null,
                cancellationToken: default);

            // Assert
            items.First().Id.ShouldNotBe(product.Id);
            items.Count().ShouldBe(2);
            items.Any(p => p.IsDeleted).ShouldBeFalse();
        }
        finally
        {
            if (product != null)
            {
                await repo.RestoreAsync(product.Id);
                await UoW.SaveChangesAsync();
            }
        }
    }
}
