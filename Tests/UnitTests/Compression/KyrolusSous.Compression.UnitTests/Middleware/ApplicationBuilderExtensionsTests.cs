namespace KyrolusSous.Compression.UnitTests.Middleware;

public class ApplicationBuilderExtensionsTests
{
    [Fact(DisplayName = "UseKyrolusResponseCompression when app is null should throw ArgumentNullException")]
    public void UseKyrolusResponseCompression_WhenAppIsNull_ShouldThrowArgumentNullException()
    {
        IApplicationBuilder app = null!;

        Should.Throw<ArgumentNullException>(() => app.UseKyrolusResponseCompression());
    }

    [Fact(DisplayName = "UseKyrolusResponseCompression should register middleware and return ApplicationBuilder instance")]
    public void UseKyrolusResponseCompression_ShouldReturnApplicationBuilder()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        var result = app.UseKyrolusResponseCompression();

        result.ShouldBeSameAs(app);
    }
}
