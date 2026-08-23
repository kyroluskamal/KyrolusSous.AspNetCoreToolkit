
namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Helpers;

public class KyrolusExceptionActivityEnricherTests
{
    [Fact(DisplayName = "Enrich should not throw when activity is null")]
    public void Enrich_WhenActivityIsNull_ShouldNotThrow()
    {
        var mapping = KyrolusExceptionMapping.Create(
            "internal_error", "Server Error", HttpStatusCode.InternalServerError, "Something went wrong");
        var context = new KyrolusErrorContext(null, null, null, null, null, null, null);
        var exception = new InvalidOperationException("boom");

        var ex = Record.Exception(() => KyrolusExceptionActivityEnricher.Enrich(null, mapping, context, exception, false));

        ex.ShouldBeNull();
    }

    [Fact(DisplayName = "Enrich should populate all activity tags and details when available")]
    public void Enrich_ShouldPopulateAllTags_WhenDetailsIncluded()
    {
        var activity = new Activity("TestSpan").Start();
        var mapping = KyrolusExceptionMapping.Create(
            "user_not_found", "Not Found", HttpStatusCode.NotFound, "User not found");

        var context = new KyrolusErrorContext(
            TraceId: "trace-123",
            CorrelationId: "corr-456",
            UserId: "user-789",
            TenantId: "tenant-001",
            Path: "/api/users/1",
            Method: "GET",
            Culture: null);

        var exception = new InvalidOperationException("User with ID 1 does not exist.");

        KyrolusExceptionActivityEnricher.Enrich(activity, mapping, context, exception, includeDetails: true);

        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("user_not_found");

        activity.GetTagItem("kyrolus.error_code").ShouldBe("user_not_found");
        activity.GetTagItem("http.status_code").ShouldBe(404);
        activity.GetTagItem("kyrolus.trace_id").ShouldBe("trace-123");
        activity.GetTagItem("kyrolus.correlation_id").ShouldBe("corr-456");
        activity.GetTagItem("enduser.id").ShouldBe("user-789");
        activity.GetTagItem("kyrolus.tenant_id").ShouldBe("tenant-001");
        activity.GetTagItem("http.target").ShouldBe("/api/users/1");
        activity.GetTagItem("http.method").ShouldBe("GET");
        activity.GetTagItem("exception.type").ShouldBe(typeof(InvalidOperationException).FullName);
        activity.GetTagItem("exception.message").ShouldBe("User with ID 1 does not exist.");
    }

    [Fact(DisplayName = "Enrich should not include exception message and stacktrace when includeDetails is false")]
    public void Enrich_ShouldNotIncludeDetails_WhenIncludeDetailsIsFalse()
    {
        var activity = new Activity("TestSpan").Start();
        var mapping = KyrolusExceptionMapping.Create(
            "bad_request", "Bad Request", HttpStatusCode.BadRequest, "Invalid data");
        var context = new KyrolusErrorContext(null, null, null, null, null, null, null);
        var exception = new ArgumentException("Secret DB info");

        KyrolusExceptionActivityEnricher.Enrich(activity, mapping, context, exception, includeDetails: false);

        activity.GetTagItem("exception.message").ShouldBeNull();
        activity.GetTagItem("exception.stacktrace").ShouldBeNull();
    }

    [Fact(DisplayName = "Enrich should not set tags for empty or whitespace context properties")]
    public void Enrich_ShouldNotSetTags_ForEmptyContextProperties()
    {
        var activity = new Activity("TestSpan").Start();
        var mapping = KyrolusExceptionMapping.Create(
            "server_error", "Error", HttpStatusCode.InternalServerError, "Error");

        var emptyContext = new KyrolusErrorContext(" ", " ", " ", " ", " ", " ", null);
        var exception = new Exception("test");

        KyrolusExceptionActivityEnricher.Enrich(activity, mapping, emptyContext, exception, false);

        activity.GetTagItem("kyrolus.trace_id").ShouldBeNull();
        activity.GetTagItem("kyrolus.correlation_id").ShouldBeNull();
        activity.GetTagItem("enduser.id").ShouldBeNull();
        activity.GetTagItem("kyrolus.tenant_id").ShouldBeNull();
        activity.GetTagItem("http.target").ShouldBeNull();
        activity.GetTagItem("http.method").ShouldBeNull();
    }
}
