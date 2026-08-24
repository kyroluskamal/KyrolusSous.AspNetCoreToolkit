namespace KyrolusSous.Compression.UnitTests.Middleware;

public class KyrolusResponseCompressionOptionsTests
{
    [Fact(DisplayName = "DefaultOptions should have sensible compression thresholds and MIME types")]
    public void DefaultOptions_ShouldHaveSensibleDefaults()
    {
        var options = new KyrolusResponseCompressionOptions();

        options.PreferredAlgorithm.ShouldBe(CompressionAlgorithm.Brotli);
        options.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
        options.MinSizeBytes.ShouldBe(1024);
        options.EnableForHttps.ShouldBeTrue();

        options.IncludedMimeTypes.ShouldContain("application/json");
        options.IncludedMimeTypes.ShouldContain("text/html");
        options.IncludedMimeTypes.ShouldContain("image/svg+xml");

        options.ExcludedMimeTypes.ShouldContain("image/jpeg");
        options.ExcludedMimeTypes.ShouldContain("image/png");
        options.ExcludedMimeTypes.ShouldContain("application/pdf");
        options.ExcludedMimeTypes.ShouldContain("application/zip");
    }

    [Fact(DisplayName = "Fluent configuration methods should set options correctly")]
    public void FluentMethods_ShouldConfigureOptionsCorrectly()
    {
        var options = new KyrolusResponseCompressionOptions();

        options.ExcludePath("api/stream")
               .ExcludePath("/hub/events")
               .IncludeMimeType("application/custom-json")
               .ExcludeMimeType("text/plain")
               .WithMinSizeBytes(2048)
               .WithPreferredAlgorithm(CompressionAlgorithm.Zstd);

        options.ExcludedPaths.ShouldContain("/api/stream");
        options.ExcludedPaths.ShouldContain("/hub/events");
        options.IncludedMimeTypes.ShouldContain("application/custom-json");
        options.IncludedMimeTypes.ShouldNotContain("text/plain");
        options.ExcludedMimeTypes.ShouldContain("text/plain");
        options.MinSizeBytes.ShouldBe(2048);
        options.PreferredAlgorithm.ShouldBe(CompressionAlgorithm.Zstd);
    }

    [Fact(DisplayName = "Fluent configuration methods with invalid arguments should throw ArgumentException")]
    public void FluentMethods_InvalidInputs_ShouldThrowArgumentException()
    {
        var options = new KyrolusResponseCompressionOptions();

        Should.Throw<ArgumentException>(() => options.ExcludePath(""));
        Should.Throw<ArgumentException>(() => options.ExcludePath("   "));
        Should.Throw<ArgumentException>(() => options.IncludeMimeType(""));
        Should.Throw<ArgumentException>(() => options.ExcludeMimeType(""));

        options.WithMinSizeBytes(-50);
        options.MinSizeBytes.ShouldBe(0);
    }
}
