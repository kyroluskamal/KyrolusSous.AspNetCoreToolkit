using KyrolusSous.Repositories.EF.Abstractions.Observer;
using Shouldly;

namespace KyrolusSous.Repositories.EF.Generator.UnitTests;

public class SampleObserverTests
{
    [Fact(DisplayName = "SampleObserver completes without throwing")]
    public async Task SampleObserver_Completes()
    {
        var obs = new SampleObserver();
        await obs.OnBeforeAsync("op", null);
        await obs.OnAfterAsync("op", new { X = 1 }, TimeSpan.FromMilliseconds(10), null);

        // If no exception thrown, consider success
        true.ShouldBeTrue();
    }
}
