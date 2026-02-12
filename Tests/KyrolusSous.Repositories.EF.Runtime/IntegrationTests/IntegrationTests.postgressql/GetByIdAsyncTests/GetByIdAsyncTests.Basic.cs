namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private static readonly IReadOnlyDictionary<string, ByIdHttpSpec> BasicSpecs = BuildBasicSpecs();

    public static TheoryData<string> BasicCases => CaseIdsFrom(BasicSpecs);

    [Theory(DisplayName = "GetByIdAsync returns entity without Include Properties")]
    [MemberData(nameof(BasicCases))]
    public async Task GetByIdAsync_Basic_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        await RunByIdHttpCase(BasicSpecs, caseId);
    }

    private static IReadOnlyDictionary<string, ByIdHttpSpec> BuildBasicSpecs()
        => new Dictionary<string, ByIdHttpSpec>
        {
            ["single-found"] = new(
                Kind: EntityKind.Product,
                SingleKey: productHeadphonesId,
                CompositeKeys: null,
                Request: null,
                ExpectedStatus: HttpStatusCode.OK,
                AssertProduct: p => p.Name.ShouldBe("Noise Cancelling Headphones"),
                AssertReview: null),

            ["composite-found"] = new(
                Kind: EntityKind.Review,
                SingleKey: null,
                CompositeKeys: CompositeKey_ProductReview,
                Request: null,
                ExpectedStatus: HttpStatusCode.OK,
                AssertProduct: null,
                AssertReview: r => r.Rating.ShouldBe(5)),

            ["single-missing"] = new(
                Kind: EntityKind.Product,
                SingleKey: productMissingId,
                CompositeKeys: null,
                Request: null,
                ExpectedStatus: HttpStatusCode.NotFound,
                AssertProduct: null,
                AssertReview: null),

            ["composite-missing"] = new(
                Kind: EntityKind.Review,
                SingleKey: null,
                CompositeKeys: CompositeKey_MissingReview,
                Request: null,
                ExpectedStatus: HttpStatusCode.NotFound,
                AssertProduct: null,
                AssertReview: null)
        };
}
