using KyrolusSous.Repositories.Marten.Abstractions.Records;
using KyrolusSous.Repositories.Marten.Abstractions.Specifications;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenRepositoryPolicyAndValidationTests
{
    public sealed class TestDocument
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }

    [Fact(DisplayName = "PageRequest: Records PageNumber and PageSize correctly")]
    public void PageRequest_RecordsPagingProperties()
    {
        var request = new MartenPageRequest(PageNumber: 3, PageSize: 25);
        request.PageNumber.ShouldBe(3);
        request.PageSize.ShouldBe(25);
    }

    [Fact(DisplayName = "PageResult: Wraps items, total count, and paging information")]
    public void PageResult_WrapsPagingInformation()
    {
        var items = new List<TestDocument> { new() { Id = Guid.NewGuid(), Title = "A" } };
        var page = new PageResult<TestDocument>(items, TotalCount: 95, PageNumber: 2, PageSize: 20);

        page.Items.Count.ShouldBe(1);
        page.TotalCount.ShouldBe(95);
        page.PageNumber.ShouldBe(2);
        page.PageSize.ShouldBe(20);
    }

    [Fact(DisplayName = "PaginationSpecification: Validates skip and take ranges")]
    public void PaginationSpecification_ThrowsOnInvalidRanges()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new KyrolusMartenPaginationSpecification<TestDocument>(-1, 10));
        Should.Throw<ArgumentOutOfRangeException>(() => new KyrolusMartenPaginationSpecification<TestDocument>(0, 0));
    }
}
