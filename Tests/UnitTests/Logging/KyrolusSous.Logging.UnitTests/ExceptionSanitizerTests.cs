using System.Collections;
using KyrolusSous.Logging.Core.Exceptions;

namespace KyrolusSous.Logging.UnitTests;

public class ExceptionSanitizerTests
{
    private readonly KyrolusExceptionSanitizer _sanitizer = new();

    [Fact(DisplayName = "ExceptionSanitizer: Sanitizes DB connection string in exception messages")]
    public void SanitizeMessage_ConnectionString_RedactsPassword()
    {
        var msg = "Connection failed to Server=sql.mycorp.com;Database=ProdDb;User Id=dbadmin;Password=SuperSecretPass123!;";
        var result = _sanitizer.SanitizeMessage(msg);

        result.ShouldNotContain("SuperSecretPass123!");
        result.ShouldContain("Password=***");
        result.ShouldContain("User Id=***");
    }

    [Fact(DisplayName = "ExceptionSanitizer: Flattens deeply nested AggregateException hierarchy")]
    public void Flatten_NestedAggregateException_FlattensAll()
    {
        var inner1 = new InvalidOperationException("First inner");
        var inner2 = new ArgumentException("Second inner");
        var nestedAgg = new AggregateException(inner1, inner2);
        var root = new AggregateException("Root error", nestedAgg, new TimeoutException("Third inner"));

        var flat = KyrolusExceptionSanitizer.Flatten(root);

        flat.Count.ShouldBe(5); // root + nestedAgg + Third inner + inner1 + inner2
        flat.ShouldContain(inner1);
        flat.ShouldContain(inner2);
    }

    [Fact(DisplayName = "ExceptionSanitizer: Flattens null exception returns empty list")]
    public void Flatten_NullException_ReturnsEmptyList()
    {
        KyrolusExceptionSanitizer.Flatten(null).ShouldBeEmpty();
    }

    [Fact(DisplayName = "ExceptionSanitizer: Sanitizes Exception Data dictionary")]
    public void SanitizeData_SanitizesSensitiveEntries()
    {
        var data = new Hashtable
        {
            ["ApiKey"] = "token=secretkey123&page=1",
            ["DbInfo"] = "Server=localhost;Password=mypassword;"
        };

        var result = _sanitizer.SanitizeData(data);

        result.ShouldNotBeNull();
        result["ApiKey"]?.ToString()!.ShouldContain("token=***");
        result["DbInfo"]?.ToString()!.ShouldContain("Password=***");
    }
}
