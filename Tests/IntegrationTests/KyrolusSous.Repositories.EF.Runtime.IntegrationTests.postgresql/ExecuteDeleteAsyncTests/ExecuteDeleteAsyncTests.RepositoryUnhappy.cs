using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteDeleteAsyncTests;

public partial class ExecuteDeleteAsyncTests
{
    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "ExecuteDeleteAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExecuteDeleteAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.ExecuteDeleteAsync(
                    x => x.Rating > 0,
                    useSplitQuery: false,
                    cancellationToken: cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await singleRepo.ExecuteDeleteAsync(
                x => x.Price > 0m,
                useSplitQuery: false,
                cancellationToken: cts.Token));
    }

    [Fact(DisplayName = "ExecuteDeleteAsync propagates bulk executor exceptions")]
    public async Task ExecuteDeleteAsync_BulkExecutorThrows_Propagates()
    {
        var customFactory = WithPolicy().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IKyrolusBulkExecutor<Product>, ThrowingProductBulkExecutor>();
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await repo.ExecuteDeleteAsync(x => x.Id == DataSeeder.productLaptopId, useSplitQuery: true));
    }

    private sealed class ThrowingProductBulkExecutor : IKyrolusBulkExecutor<Product>
    {
        public Task<int> ExecuteDeleteAsync(Expression<Func<Product, bool>>? filter, bool useSplitQuery, CancellationToken cancellationToken)
            => throw new InvalidOperationException("bulk delete failure");

        public Task<int> ExecuteUpdateAsync(Expression<Func<Product, bool>>? filter, Action<UpdateSettersBuilder<Product>> setPropertyCalls, bool useSplitQuery, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> BulkInsertAsync(IEnumerable<Product> entities, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> BulkUpsertAsync(IEnumerable<Product> entities, Expression<Func<Product, bool>> matchOn, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
