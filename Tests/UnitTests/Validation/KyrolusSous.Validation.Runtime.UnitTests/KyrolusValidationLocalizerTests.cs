namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationLocalizerTests
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> CultureMapData = new()
    {
        ["ar-EG"] = new Dictionary<string, string>
        {
            ["ERR_UNDERAGE"] = "عفواً، يجب أن يكون عمرك 18 عاماً على الأقل",
            ["REQUIRED_FIELD"] = "هذا الحقل مطلوب"
        },
        ["en-US"] = new Dictionary<string, string>
        {
            ["ERR_UNDERAGE"] = "Sorry, you must be at least 18 years old",
            ["REQUIRED_FIELD"] = "This field is required"
        }
    };
    private static readonly Dictionary<string, string> InvariantMapData = new()
    {
        ["ERR_UNDERAGE"] = "Underage error",
        ["REQUIRED_FIELD"] = "Required field error"
    };
    #region KyrolusNullValidationErrorLocalizer
    [Fact(DisplayName = "KyrolusNullValidationErrorLocalizer should return the errors without translation")]
    public void KyrolusNullValidationErrorLocalizer_ShouldReturn_Errors_Without_Translations()
    {
        var failures = new KyrolusValidationFailure("name", "name is required");
        var localizer = new KyrolusNullValidationErrorLocalizer();
        localizer.Localize(failures).ShouldBe("name is required");
    }
    #endregion

    #region KyrolusDictionaryValidationErrorLocalizer
    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should throw error if the culterMaps is null")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldThrowErrorIfCultureMapsIsNull(){
        var exception = Should.Throw<ArgumentNullException>(()=> new KyrolusDictionaryValidationErrorLocalizer(null!));
        exception.ParamName.ShouldBe("cultureMaps");
        exception.Message.ShouldContain("You should add at least one culture map");
    }
    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should return the error translation by messageKey if found")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldReturnErrorTranslationByMessageKey()
    {
        var localizer = new KyrolusDictionaryValidationErrorLocalizer(CultureMapData);
        var failures = new KyrolusValidationFailure("name", "This field is required", MessageKey: "REQUIRED_FIELD");

        var error = localizer.Localize(failures, new CultureInfo("ar-EG"));
        error.ShouldBe("هذا الحقل مطلوب");
    }
    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should return the error translation by ErrorCode if found")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldReturnErrorTranslationBy_ErrorCode()
    {
        var localizer = new KyrolusDictionaryValidationErrorLocalizer(CultureMapData);
        var failures = new KyrolusValidationFailure("name", "This field is required", ErrorCode: "REQUIRED_FIELD");

        var error = localizer.Localize(failures, new CultureInfo("ar-EG"));
        error.ShouldBe("هذا الحقل مطلوب");
    }
    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should return the error original message if the invariantMap is null and the lang is not found in the culturemap")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldReturnOriginalError_if_InvariantMapIsNUll_and_Lang_doesnot_exist()
    {
        var localizer = new KyrolusDictionaryValidationErrorLocalizer(CultureMapData);
        var failures = new KyrolusValidationFailure("name", "name is required", ErrorCode: "REQUIRED_FIELD");

        var error = localizer.Localize(failures, new CultureInfo("fr-FR"));
        error.ShouldBe("name is required");
    }
    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should return the error original message if the key or errorcode is not found")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldReturnOriginalError_if_KeyOrErrorCode_is_Not_Found()
    {
        var localizer = new KyrolusDictionaryValidationErrorLocalizer(CultureMapData);
        var failures = new KyrolusValidationFailure("name", "name is required");

        var error = localizer.Localize(failures, new CultureInfo("fr-FR"));
        error.ShouldBe("name is required");
    }
    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should return the error translation from invariantMap if the language ")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldReturnErrorTranslation_FromInvariantMap_IF_LangDoesnot_exist()
    {
        var localizer = new KyrolusDictionaryValidationErrorLocalizer(CultureMapData, InvariantMapData);
        var failures = new KyrolusValidationFailure("name", "This field is required", ErrorCode: "REQUIRED_FIELD");

        var error = localizer.Localize(failures, new CultureInfo("fr-FR"));
        error.ShouldBe("Required field error");
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should use CurrentUICulture when culture parameter is null")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldUseCurrentUICulture_WhenCultureIsNull()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar-EG");
            var localizer = new KyrolusDictionaryValidationErrorLocalizer(CultureMapData);
            var failure = new KyrolusValidationFailure("name", "This field is required", MessageKey: "REQUIRED_FIELD");

            var error = localizer.Localize(failure);
            error.ShouldBe("هذا الحقل مطلوب");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact(DisplayName = "KyrolusDictionaryValidationErrorLocalizer should return error translation by ErrorMessage if MessageKey and ErrorCode are null")]
    public void KyrolusDictionaryValidationErrorLocalizer_ShouldReturnErrorTranslationBy_ErrorMessage()
    {
        var customCultureMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ar-EG"] = new Dictionary<string, string>
            {
                ["This field is required"] = "هذا الحقل مطلوب"
            }
        };

        var localizer = new KyrolusDictionaryValidationErrorLocalizer(customCultureMap);
        var failure = new KyrolusValidationFailure("name", "This field is required");

        var error = localizer.Localize(failure, new CultureInfo("ar-EG"));
        error.ShouldBe("هذا الحقل مطلوب");
    }
    #endregion
}
