namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public class KyrolusDefaultErrorMetadataSanitizerTests
{
    private static readonly KyrolusErrorContext TestErrorContext = new(
        TraceId: "trace-123",
        CorrelationId: "corr-456",
        UserId: "user-789",
        TenantId: "tenant-101",
        Path: "/api/checkout",
        Method: "POST",
        Culture: null);

    [Fact(DisplayName = "Sanitize should return original dictionary when SanitizeMetadata is false")]
    public void Sanitize_Should_Return_Original_When_SanitizeMetadata_Is_False()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions
        {
            SanitizeMetadata = false
        });
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(options);

        var metadata = new Dictionary<string, object?>
        {
            ["password"] = "P@ssword123",
            ["token"] = "secret-token",
            ["userId"] = 42
        };

        var result = sanitizer.Sanitize(metadata, TestErrorContext);

        result.ShouldBeSameAs(metadata);
        result.ShouldContainKey("password");
        result.ShouldContainKey("token");
        result.ShouldContainKey("userId");
    }

    [Fact(DisplayName = "Sanitize should return empty dictionary immediately when metadata is empty")]
    public void Sanitize_Should_Return_Same_When_Metadata_Is_Empty()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(options);

        var metadata = new Dictionary<string, object?>();

        var result = sanitizer.Sanitize(metadata, TestErrorContext);

        result.ShouldBeSameAs(metadata);
    }

    [Fact(DisplayName = "Sanitize should return non-null dictionary when metadata is null")]
    public void Sanitize_Should_Return_NonNull_When_Metadata_Is_Null()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(options);

        var result = sanitizer.Sanitize(null, TestErrorContext);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "Sanitize should remove all default sensitive keys and partial sensitive substrings")]
    public void Sanitize_Should_Remove_Sensitive_Keys_CaseInsensitively()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(options);

        var metadata = new Dictionary<string, object?>
        {
            ["PASSWORD"] = "secret1",
            ["user_password"] = "secret1-sub",
            ["db_secret_key"] = "db-pass",
            ["Pwd"] = "secret2",
            ["secret"] = "secret3",
            ["TOKEN"] = "token1",
            ["auth_token"] = "jwt-val",
            ["Authorization"] = "Bearer token",
            ["cookie"] = "session=123",
            ["Set-Cookie"] = "session=123",
            ["api-key"] = "key1",
            ["custom_apiKey_header"] = "key-sub",
            ["APIKEY"] = "key2",
            ["access_token"] = "token2",
            ["Refresh_Token"] = "token3",
            ["JWT"] = "jwt.token.here",
            ["userId"] = 100,
            ["orderId"] = "ORD-555",
            ["attempt"] = 3
        };

        var result = sanitizer.Sanitize(metadata, TestErrorContext);

        result.Count.ShouldBe(3);
        result.ShouldContainKey("userId");
        result.ShouldContainKey("orderId");
        result.ShouldContainKey("attempt");

        result.ShouldNotContainKey("PASSWORD");
        result.ShouldNotContainKey("user_password");
        result.ShouldNotContainKey("db_secret_key");
        result.ShouldNotContainKey("Pwd");
        result.ShouldNotContainKey("secret");
        result.ShouldNotContainKey("TOKEN");
        result.ShouldNotContainKey("auth_token");
        result.ShouldNotContainKey("Authorization");
        result.ShouldNotContainKey("cookie");
        result.ShouldNotContainKey("Set-Cookie");
        result.ShouldNotContainKey("api-key");
        result.ShouldNotContainKey("custom_apiKey_header");
        result.ShouldNotContainKey("APIKEY");
        result.ShouldNotContainKey("access_token");
        result.ShouldNotContainKey("Refresh_Token");
        result.ShouldNotContainKey("JWT");
    }

    [Fact(DisplayName = "Sanitize should only keep allowed keys when MetadataAllowList is provided")]
    public void Sanitize_Should_Only_Keep_AllowListed_Keys()
    {
        var options = new KyrolusExceptionHandlingOptions
        {
            MetadataAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "userId",
                "orderId"
            }
        };
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(Options.Create(options));

        var metadata = new Dictionary<string, object?>
        {
            ["USERID"] = "USR-1",
            ["OrderId"] = "ORD-2",
            ["otherSafeKey"] = "safeValue",
            ["password"] = "secretPass"
        };

        var result = sanitizer.Sanitize(metadata, TestErrorContext);

        result.Count.ShouldBe(2);
        result.ShouldContainKey("USERID");
        result.ShouldContainKey("OrderId");
        result.ShouldNotContainKey("otherSafeKey");
        result.ShouldNotContainKey("password");
    }

    [Fact(DisplayName = "Sanitize should fall back to sensitive key filtering when MetadataAllowList is empty")]
    public void Sanitize_Should_Fallback_To_Sensitive_Filter_When_AllowList_Is_Empty()
    {
        var options = new KyrolusExceptionHandlingOptions
        {
            MetadataAllowList = []
        };
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(Options.Create(options));

        var metadata = new Dictionary<string, object?>
        {
            ["safeKey"] = "value1",
            ["password"] = "secret"
        };

        var result = sanitizer.Sanitize(metadata, TestErrorContext);

        result.Count.ShouldBe(1);
        result.ShouldContainKey("safeKey");
        result.ShouldNotContainKey("password");
    }

    [Fact(DisplayName = "Sanitize should remove custom sensitive keys added to options")]
    public void Sanitize_Should_Remove_Custom_Sensitive_Keys()
    {
        var options = new KyrolusExceptionHandlingOptions();
        options.SensitiveMetadataKeys.Add("creditCard");
        options.SensitiveMetadataKeys.Add("ssn");

        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(Options.Create(options));

        var metadata = new Dictionary<string, object?>
        {
            ["creditCard"] = "4111-2222-3333-4444",
            ["SSN"] = "123-45-6789",
            ["customerName"] = "John Doe"
        };

        var result = sanitizer.Sanitize(metadata, TestErrorContext);

        result.Count.ShouldBe(1);
        result.ShouldContainKey("customerName");
        result.ShouldNotContainKey("creditCard");
        result.ShouldNotContainKey("SSN");
    }
}
