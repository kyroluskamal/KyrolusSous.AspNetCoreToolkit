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
        p.GlobalQueryFilter.ShouldBeNull();
        p.ConcurrencyRetryCount.ShouldBe(0);
        p.ConcurrencyRetryDelay.ShouldBeNull();
        p.DefaultPageSize.ShouldBeNull();
    }

    [Fact(DisplayName = "Policy can hold custom values")]
    public void Policy_AllowsCustomValues()
    {
        var p = new KyrolusRepositoryPolicy
        {
            AsNoTrackingDefault = true,
            UseSplitQueryDefault = false,
            EnableSoftDeleteDefault = true,
            ConcurrencyRetryCount = 3,
            ConcurrencyRetryDelay = TimeSpan.FromSeconds(2),
            DefaultPageSize = 25,
            GlobalQueryFilter = (Func<IQueryable<int>, IQueryable<int>>)(q => q.Where(x => x > 0))
        };

        p.AsNoTrackingDefault.ShouldBe(true);
        p.UseSplitQueryDefault.ShouldBe(false);
        p.EnableSoftDeleteDefault.ShouldBe(true);
        p.ConcurrencyRetryCount.ShouldBe(3);
        p.ConcurrencyRetryDelay.ShouldBe(TimeSpan.FromSeconds(2));
        p.DefaultPageSize.ShouldBe(25);
        p.GlobalQueryFilter.ShouldNotBeNull();
    }
}
