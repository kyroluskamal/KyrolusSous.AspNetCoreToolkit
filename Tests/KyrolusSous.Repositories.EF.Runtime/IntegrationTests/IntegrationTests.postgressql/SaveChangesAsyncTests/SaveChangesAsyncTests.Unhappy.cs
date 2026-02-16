namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SaveChangesAsyncTests;

public partial class SaveChangesAsyncTests
{
    [Fact(DisplayName = "SaveChangesAsync throws DbUpdateException for invalid changes")]
    public async Task SaveChangesAsync_InvalidChanges_Throws()
    {
        var id = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        db.Products.Add(CreateValidProduct(
            id: id,
            storeId: Guid.NewGuid(),
            name: "save-invalid-store",
            sku: $"save-invalid-{id:N}"));

        await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
        (await FindProductAsync(id)).ShouldBeNull();
    }

    [Fact(DisplayName = "SaveChangesAsync throws OperationCanceledException for canceled token")]
    public async Task SaveChangesAsync_CanceledToken_Throws()
    {
        var id = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        db.Products.Add(CreateValidProduct(id: id, name: "save-canceled", sku: $"save-canceled-{id:N}"));

        await Should.ThrowAsync<OperationCanceledException>(async () => await uow.SaveChangesAsync(cts.Token));
        (await FindProductAsync(id)).ShouldBeNull();
    }
}
