namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

internal static class RuntimeQueryHelperTestData
{
    internal static readonly Guid CategoryA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid CategoryB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid Id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid Id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid Id3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    internal static List<TestEntity> CreateEntities()
        =>
        [
            new TestEntity
            {
                Id = Id1,
                IntValue = 5,
                NullableInt = null,
                DecimalValue = 10.5m,
                NullableDecimal = null,
                IsActive = true,
                Name = "Alpha",
                Notes = null,
                DateOnlyValue = new DateOnly(2024, 6, 1),
                TimeOnlyValue = new TimeOnly(10, 30),
                DateTimeValue = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                DateTimeOffsetValue = DateTimeOffset.Parse("2024-06-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                Status = TestStatus.Pending,
                Scores = [1, 2, 3],
                Items = [new NestedEntity { Rating = 5, CategoryId = CategoryA }]
            },
            new TestEntity
            {
                Id = Id2,
                IntValue = 10,
                NullableInt = 3,
                DecimalValue = 25m,
                NullableDecimal = 0.5m,
                IsActive = false,
                Name = "Beta",
                Notes = "note",
                DateOnlyValue = new DateOnly(2024, 7, 1),
                TimeOnlyValue = new TimeOnly(9, 0),
                DateTimeValue = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                DateTimeOffsetValue = DateTimeOffset.Parse("2024-07-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                Status = TestStatus.Active,
                Scores = [2, 4],
                Items = [new NestedEntity { Rating = 3, CategoryId = CategoryB }]
            },
            new TestEntity
            {
                Id = Id3,
                IntValue = 20,
                NullableInt = 7,
                DecimalValue = 100m,
                NullableDecimal = 1.5m,
                IsActive = true,
                Name = "Gamma",
                Notes = "other",
                DateOnlyValue = new DateOnly(2025, 1, 1),
                TimeOnlyValue = new TimeOnly(14, 0),
                DateTimeValue = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                DateTimeOffsetValue = DateTimeOffset.Parse("2025-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                Status = TestStatus.Archived,
                Scores = [5],
                Items = [
                    new NestedEntity { Rating = 5, CategoryId = CategoryA },
                    new NestedEntity { Rating = 4, CategoryId = CategoryB }
                ]
            }
        ];

    internal static List<TestEntity> ApplyFilter(QueryRequest request)
    {
        var helper = new RuntimeQueryHelper<TestEntity>();
        var filter = helper.BuildFilter(request);
        filter.ShouldNotBeNull();
        var predicate = filter!.Compile();
        return CreateEntities().Where(predicate).ToList();
    }

    internal static List<TestEntity> ApplyOrderBy(QueryRequest request)
    {
        var helper = new RuntimeQueryHelper<TestEntity>();
        var orderBy = helper.BuildOrderBy(request);
        orderBy.ShouldNotBeNull();
        return orderBy!(CreateEntities().AsQueryable()).ToList();
    }

    internal sealed class TestEntity
    {
        public Guid Id { get; set; }
        public int IntValue { get; set; }
        public int? NullableInt { get; set; }
        public decimal DecimalValue { get; set; }
        public decimal? NullableDecimal { get; set; }
        public bool IsActive { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateOnly DateOnlyValue { get; set; }
        public TimeOnly TimeOnlyValue { get; set; }
        public DateTime DateTimeValue { get; set; }
        public DateTimeOffset DateTimeOffsetValue { get; set; }
        public TestStatus Status { get; set; }
        public List<int> Scores { get; set; } = [];
        public List<NestedEntity> Items { get; set; } = [];
    }

    internal sealed class NestedEntity
    {
        public int Rating { get; set; }
        public Guid CategoryId { get; set; }
    }

    internal enum TestStatus
    {
        Pending,
        Active,
        Archived
    }
}
