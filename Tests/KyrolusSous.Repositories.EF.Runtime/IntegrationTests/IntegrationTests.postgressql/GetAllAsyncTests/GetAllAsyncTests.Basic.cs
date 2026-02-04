namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Fact(DisplayName = "GetAllAsync returns all entities without Include Properties or filters or ordering options")]
    public async Task GetAllAsync_NoIncludeNoFilterNoOrder_ReturnsAllEntities()
    {
        var (response, reviews, _) = await ArrangeAndActUseingHttpForListAsync<Review>();
        // Assert
        response.EnsureSuccessStatusCode();
        reviews.ShouldNotBeNull();
        reviews.ShouldHaveSingleItem();
    }
}
