using KyrolusSous.Repositories.EF.Abstractions.Policy;
using Shouldly;

namespace KyrolusSous.Repositories.EF.Generator.UnitTests;

public class KyrolusRepositoryPolicyTests
{
    [Fact(DisplayName = "Default policy has nulls and zero retries")]
    public void DefaultPolicy_HasExpectedDefaults()
    {
        var p = KyrolusRepositoryPolicy.Default;

        p.AsNoTrackingDefault.ShouldBeNull();
        p.UseSplitQueryDefault.ShouldBeNull();
        p.EnableSoftDeleteDefault.ShouldBeNull();
        p.ConcurrencyRetryCount.ShouldBe(0);
        p.ConcurrencyRetryDelay.ShouldBeNull();
        p.DefaultPageSize.ShouldBeNull();

        // No filters by default
        p.GetGlobalQueryFilter<int>().ShouldBeNull();
    }

    [Fact(DisplayName = "Default policy returns a new instance each time (not shared)")]
    public void DefaultPolicy_IsNotSingleton()
    {
        var p1 = KyrolusRepositoryPolicy.Default;
        var p2 = KyrolusRepositoryPolicy.Default;

        ReferenceEquals(p1, p2).ShouldBeTrue();
    }

    [Fact(DisplayName = "Policy can hold custom values and per-entity global filters")]
    public void Policy_AllowsCustomValues_AndGlobalFilters()
    {
        var p = new KyrolusRepositoryPolicy
        {
            AsNoTrackingDefault = true,
            UseSplitQueryDefault = false,
            EnableSoftDeleteDefault = true,
            ConcurrencyRetryCount = 3,
            ConcurrencyRetryDelay = TimeSpan.FromSeconds(2),
            DefaultPageSize = 25,
        };

        // Add 2 filters for int (pipeline)
        p.AddGlobalQueryFilter<int>(q => q.Where(x => x > 0));
        p.AddGlobalQueryFilter<int>(q => q.Where(x => x % 2 == 0));

        p.AsNoTrackingDefault.ShouldBe(true);
        p.UseSplitQueryDefault.ShouldBe(false);
        p.EnableSoftDeleteDefault.ShouldBe(true);
        p.ConcurrencyRetryCount.ShouldBe(3);
        p.ConcurrencyRetryDelay.ShouldBe(TimeSpan.FromSeconds(2));
        p.DefaultPageSize.ShouldBe(25);

        var f = p.GetGlobalQueryFilter<int>();
        f.ShouldNotBeNull();

        // Verify composition works (x > 0) then (x even)
        var input = new[] { -2, -1, 0, 1, 2, 3, 4 }.AsQueryable();
        var result = f!(input).ToArray();
        result.ShouldBe([2, 4]);

        // Different entity type => no filter
        p.GetGlobalQueryFilter<string>().ShouldBeNull();
    }
}
