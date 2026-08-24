using KyrolusSous.Logging.Core.Redaction;

namespace KyrolusSous.Logging.UnitTests;

public class StringRedactorTests
{
    private readonly KyrolusStringRedactor _redactor = new();

    [Fact(DisplayName = "Redactor: Redacts JWT tokens")]
    public void Redact_JwtToken_ReplacedWithMask()
    {
        var input = "User token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozG5Vo1tYe99";
        var result = _redactor.Redact(input);

        result.ShouldNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");
        result.ShouldContain("***");
    }

    [Fact(DisplayName = "Redactor: Redacts Bearer authorization header")]
    public void Redact_BearerHeader_ReplacedWithMask()
    {
        var input = "Authorization: Bearer my-super-secret-api-token-12345";
        var result = _redactor.Redact(input);

        result.ShouldBe("Authorization: Bearer ***");
    }

    [Fact(DisplayName = "Redactor: Redacts sensitive URL query parameters")]
    public void Redact_UrlQueryParams_ReplacedWithMask()
    {
        var input = "https://api.domain.com/v1/checkout?token=secret123&client_secret=topsecret&page=1";
        var result = _redactor.Redact(input);

        result.ShouldContain("?token=***");
        result.ShouldContain("&client_secret=***");
        result.ShouldContain("&page=1");
    }

    [Fact(DisplayName = "Redactor: Redacts valid Luhn credit cards and leaves non-cards intact")]
    public void Redact_CreditCards_ValidLuhnMasked_InvalidKept()
    {
        // 49927398716 is a known valid Luhn number (11 digits, but let's test a standard 16 digit: 4532 0151 1283 0366)
        var validCard = "My card number is 4532 0151 1283 0366 for payment";
        var result = _redactor.Redact(validCard);
        result.ShouldContain("***");
        result.ShouldNotContain("4532 0151 1283 0366");

        var invalidCard = "Order number is 1234 5678 9012 3456 not a card";
        var invalidResult = _redactor.Redact(invalidCard);
        invalidResult.ShouldContain("1234 5678 9012 3456");
    }

    [Fact(DisplayName = "Redactor: Returns empty or unchanged string when input is null or clean")]
    public void Redact_NullOrClean_ReturnsExpected()
    {
        _redactor.Redact(null).ShouldBe(string.Empty);
        _redactor.Redact("Clean ordinary log message").ShouldBe("Clean ordinary log message");
    }
}
