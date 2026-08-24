using KyrolusSous.Repositories.Marten.Abstractions.Includes;
using Marten.Linq;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenIncludeGraphTests
{
    public sealed class OrderDoc
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
    }

    [Fact(DisplayName = "IncludeGraph: Applies configured include actions sequentially")]
    public void IncludeGraph_AppliesConfiguredActions()
    {
        var graph = new KyrolusMartenIncludeGraph<OrderDoc>();
        var called = false;

        graph.Include(q =>
        {
            called = true;
        });

        var query = Substitute.For<IMartenQueryable<OrderDoc>>();
        var result = graph.Apply(query);

        result.ShouldBeSameAs(query);
        called.ShouldBeTrue();
    }
}
