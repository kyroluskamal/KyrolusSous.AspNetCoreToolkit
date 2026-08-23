using System.IO;
using System.Net.Sockets;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Helpers;

public class KyrolusMetadataExtractorTests
{
    [Fact(DisplayName = "Extract should return null when exception contains no metadata or data")]
    public void Extract_Should_Return_Null_When_Empty()
    {
        var ex = new Exception("Simple error");
        var result = KyrolusMetadataExtractor.Extract(ex);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "Extract should merge explicit metadata and exception data")]
    public void Extract_Should_Merge_Explicit_And_Exception_Data()
    {
        var ex = new Exception("Error with data");
        ex.Data["CustomDataKey"] = "CustomDataVal";

        var explicitMetadata = new Dictionary<string, object?>
        {
            ["ExplicitKey"] = "ExplicitVal"
        };

        var result = KyrolusMetadataExtractor.Extract(ex, explicitMetadata);

        result.ShouldNotBeNull();
        result["ExplicitKey"].ShouldBe("ExplicitVal");
        result["CustomDataKey"].ShouldBe("CustomDataVal");
    }

    [Fact(DisplayName = "Extract should extract from IKyrolusExceptionWithMetadata interface")]
    public void Extract_Should_Extract_From_IKyrolusExceptionWithMetadata()
    {
        var ex = new TestExceptionWithMetadata(new Dictionary<string, object?>
        {
            ["featureKey"] = "featureVal"
        });

        var result = KyrolusMetadataExtractor.Extract(ex);

        result.ShouldNotBeNull();
        result["featureKey"].ShouldBe("featureVal");
    }

    [Fact(DisplayName = "Extract should extract CultureNotFoundException metadata")]
    public void Extract_Should_Extract_CultureNotFoundException_Metadata()
    {
        var ex = new CultureNotFoundException("cultureParam", "invalid-culture-123", "Culture not found");

        var result = KyrolusMetadataExtractor.Extract(ex);

        result.ShouldNotBeNull();
        result["paramName"].ShouldBe("cultureParam");
        result["invalidCultureName"].ShouldBe("invalid-culture-123");
    }

    [Fact(DisplayName = "Extract should extract ArgumentOutOfRangeException metadata")]
    public void Extract_Should_Extract_ArgumentOutOfRangeException_Metadata()
    {
        var ex = new ArgumentOutOfRangeException("age", 15, "Age must be >= 18");

        var result = KyrolusMetadataExtractor.Extract(ex);

        result.ShouldNotBeNull();
        result["paramName"].ShouldBe("age");
        result["actualValue"].ShouldBe(15);
    }

    [Fact(DisplayName = "Extract should extract SocketException metadata")]
    public void Extract_Should_Extract_SocketException_Metadata()
    {
        var ex = new SocketException((int)SocketError.ConnectionRefused);

        var result = KyrolusMetadataExtractor.Extract(ex);

        result.ShouldNotBeNull();
        result["socketErrorCode"].ShouldBe(SocketError.ConnectionRefused.ToString());
        result["nativeErrorCode"].ShouldBe((int)SocketError.ConnectionRefused);
    }

    [Fact(DisplayName = "Extract should extract HttpRequestException metadata")]
    public void Extract_Should_Extract_HttpRequestException_Metadata()
    {
        var ex = new HttpRequestException("HTTP request failed", null, HttpStatusCode.ServiceUnavailable);

        var result = KyrolusMetadataExtractor.Extract(ex);

        result.ShouldNotBeNull();
        result["httpStatusCode"].ShouldBe(503);
        result.ShouldContainKey("httpRequestError");
    }

    [Fact(DisplayName = "Extract should extract JsonException metadata")]
    public void Extract_Should_Extract_JsonException_Metadata()
    {
        var ex = new JsonException("Invalid JSON token", "$.users[0].name", 12, 45);

        var result = KyrolusMetadataExtractor.Extract(ex);

        result.ShouldNotBeNull();
        result["lineNumber"].ShouldBe(12L);
        result["bytePositionInLine"].ShouldBe(45L);
        result["jsonPath"].ShouldBe("$.users[0].name");
    }

    [Fact(DisplayName = "Extract should extract FileNotFoundException and DirectoryNotFoundException metadata")]
    public void Extract_Should_Extract_IO_Exceptions_Metadata()
    {
        var fileEx = new FileNotFoundException("File not found", "missing_config.json");
        var fileResult = KyrolusMetadataExtractor.Extract(fileEx);

        fileResult.ShouldNotBeNull();
        fileResult["fileName"].ShouldBe("missing_config.json");

        var dirEx = new DirectoryNotFoundException("Directory was not found");
        var dirResult = KyrolusMetadataExtractor.Extract(dirEx);

        dirResult.ShouldNotBeNull();
        dirResult["message"].ShouldBe("Directory was not found");
    }

    private sealed class TestExceptionWithMetadata(IReadOnlyDictionary<string, object?> metadata)
        : Exception, IKyrolusExceptionWithMetadata
    {
        public IReadOnlyDictionary<string, object?> GetMetadata() => metadata;
    }
}
