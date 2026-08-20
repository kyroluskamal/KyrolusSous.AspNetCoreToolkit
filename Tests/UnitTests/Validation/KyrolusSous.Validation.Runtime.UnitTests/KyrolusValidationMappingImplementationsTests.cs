namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationMappingImplementationsTests
{
    #region KyrolusDelegateValidationErrorCodeMapper
    [Fact(DisplayName = "KyrolusDelegateValidationErrorCodeMapper should throw ArgumentNullException when resolver is null")]
    public void KyrolusDelegateValidationErrorCodeMapper_ShouldThrowArgumentNullException_WhenResolverIsNull()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KyrolusDelegateValidationErrorCodeMapper(null!));
        exception.ParamName.ShouldBe("resolver");
    }

    [Fact(DisplayName = "KyrolusDelegateValidationErrorCodeMapper should execute resolver delegate")]
    public void KyrolusDelegateValidationErrorCodeMapper_ShouldExecuteResolver()
    {
        var mapper = new KyrolusDelegateValidationErrorCodeMapper((failure, ctx) => $"MAPPED_{failure.ErrorCode}");
        var failure = new KyrolusValidationFailure("Age", "Invalid age", ErrorCode: "ERR_AGE");
        var context = KyrolusValidationContext.Default;

        var result = mapper.MapErrorCode(failure, context);

        result.ShouldBe("MAPPED_ERR_AGE");
    }
    #endregion

    #region KyrolusDelegateValidationFieldPathMapper
    [Fact(DisplayName = "KyrolusDelegateValidationFieldPathMapper should throw ArgumentNullException when resolver is null")]
    public void KyrolusDelegateValidationFieldPathMapper_ShouldThrowArgumentNullException_WhenResolverIsNull()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KyrolusDelegateValidationFieldPathMapper(null!));
        exception.ParamName.ShouldBe("resolver");
    }

    [Fact(DisplayName = "KyrolusDelegateValidationFieldPathMapper should execute resolver delegate")]
    public void KyrolusDelegateValidationFieldPathMapper_ShouldExecuteResolver()
    {
        var mapper = new KyrolusDelegateValidationFieldPathMapper((failure, ctx) => failure.PropertyName?.ToLowerInvariant());
        var failure = new KyrolusValidationFailure("DateOfBirth", "Invalid date");
        var context = KyrolusValidationContext.Default;

        var result = mapper.MapFieldPath(failure, context);

        result.ShouldBe("dateofbirth");
    }
    #endregion

    #region KyrolusDictionaryValidationErrorCodeMapper
    [Fact(DisplayName = "KyrolusDictionaryValidationErrorCodeMapper should throw ArgumentNullException when map is null")]
    public void KyrolusDictionaryValidationErrorCodeMapper_ShouldThrowArgumentNullException_WhenMapIsNull()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KyrolusDictionaryValidationErrorCodeMapper(null!));
        exception.ParamName.ShouldBe("map");
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationErrorCodeMapper should map by ErrorCode first")]
    public void KyrolusDictionaryValidationErrorCodeMapper_ShouldMapByErrorCodeFirst()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["ERR_CODE"] = "MAPPED_BY_ERROR_CODE",
            ["MSG_KEY"] = "MAPPED_BY_MESSAGE_KEY",
            ["PropName"] = "MAPPED_BY_PROP_NAME"
        };
        var mapper = new KyrolusDictionaryValidationErrorCodeMapper(dictionary);
        var failure = new KyrolusValidationFailure("PropName", "Invalid", ErrorCode: "ERR_CODE", MessageKey: "MSG_KEY");

        var result = mapper.MapErrorCode(failure, KyrolusValidationContext.Default);

        result.ShouldBe("MAPPED_BY_ERROR_CODE");
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationErrorCodeMapper should map by MessageKey when ErrorCode is absent or unmapped")]
    public void KyrolusDictionaryValidationErrorCodeMapper_ShouldMapByMessageKey_WhenErrorCodeUnmapped()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["MSG_KEY"] = "MAPPED_BY_MESSAGE_KEY",
            ["PropName"] = "MAPPED_BY_PROP_NAME"
        };
        var mapper = new KyrolusDictionaryValidationErrorCodeMapper(dictionary);
        var failure = new KyrolusValidationFailure("PropName", "Invalid", ErrorCode: "UNMAPPED_CODE", MessageKey: "MSG_KEY");

        var result = mapper.MapErrorCode(failure, KyrolusValidationContext.Default);

        result.ShouldBe("MAPPED_BY_MESSAGE_KEY");
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationErrorCodeMapper should map by PropertyName when ErrorCode and MessageKey are absent or unmapped")]
    public void KyrolusDictionaryValidationErrorCodeMapper_ShouldMapByPropertyName_WhenErrorCodeAndMessageKeyUnmapped()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["PropName"] = "MAPPED_BY_PROP_NAME"
        };
        var mapper = new KyrolusDictionaryValidationErrorCodeMapper(dictionary);
        var failure = new KyrolusValidationFailure("PropName", "Invalid");

        var result = mapper.MapErrorCode(failure, KyrolusValidationContext.Default);

        result.ShouldBe("MAPPED_BY_PROP_NAME");
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationErrorCodeMapper should return null when no key matches in map")]
    public void KyrolusDictionaryValidationErrorCodeMapper_ShouldReturnNull_WhenNoKeyMatches()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["OTHER_KEY"] = "OTHER_VAL"
        };
        var mapper = new KyrolusDictionaryValidationErrorCodeMapper(dictionary);
        var failure = new KyrolusValidationFailure("PropName", "Invalid", ErrorCode: "ERR_CODE");

        var result = mapper.MapErrorCode(failure, KyrolusValidationContext.Default);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationErrorCodeMapper should return null when mapped value is whitespace")]
    public void KyrolusDictionaryValidationErrorCodeMapper_ShouldReturnNull_WhenMappedValueIsWhitespace()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["ERR_CODE"] = "   "
        };
        var mapper = new KyrolusDictionaryValidationErrorCodeMapper(dictionary);
        var failure = new KyrolusValidationFailure("PropName", "Invalid", ErrorCode: "ERR_CODE");

        var result = mapper.MapErrorCode(failure, KyrolusValidationContext.Default);

        result.ShouldBeNull();
    }
    #endregion

    #region KyrolusDictionaryValidationFieldPathMapper
    [Fact(DisplayName = "KyrolusDictionaryValidationFieldPathMapper should throw ArgumentNullException when map is null")]
    public void KyrolusDictionaryValidationFieldPathMapper_ShouldThrowArgumentNullException_WhenMapIsNull()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KyrolusDictionaryValidationFieldPathMapper(null!));
        exception.ParamName.ShouldBe("map");
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationFieldPathMapper should return null when PropertyName is null or whitespace")]
    public void KyrolusDictionaryValidationFieldPathMapper_ShouldReturnNull_WhenPropertyNameIsNullOrWhitespace()
    {
        var dictionary = new Dictionary<string, string> { ["Age"] = "user.age" };
        var mapper = new KyrolusDictionaryValidationFieldPathMapper(dictionary);
        var failure = new KyrolusValidationFailure("   ", "Invalid age");

        var result = mapper.MapFieldPath(failure, KyrolusValidationContext.Default);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationFieldPathMapper should map PropertyName when found in map")]
    public void KyrolusDictionaryValidationFieldPathMapper_ShouldMapPropertyName_WhenFoundInMap()
    {
        var dictionary = new Dictionary<string, string> { ["DateOfBirth"] = "user.date_of_birth" };
        var mapper = new KyrolusDictionaryValidationFieldPathMapper(dictionary);
        var failure = new KyrolusValidationFailure("DateOfBirth", "Invalid date");

        var result = mapper.MapFieldPath(failure, KyrolusValidationContext.Default);

        result.ShouldBe("user.date_of_birth");
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationFieldPathMapper should return null when PropertyName is not found in map")]
    public void KyrolusDictionaryValidationFieldPathMapper_ShouldReturnNull_WhenNotFoundInMap()
    {
        var dictionary = new Dictionary<string, string> { ["OtherProperty"] = "user.other" };
        var mapper = new KyrolusDictionaryValidationFieldPathMapper(dictionary);
        var failure = new KyrolusValidationFailure("DateOfBirth", "Invalid date");

        var result = mapper.MapFieldPath(failure, KyrolusValidationContext.Default);

        result.ShouldBeNull();
    }
    #endregion
}
